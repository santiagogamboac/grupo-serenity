#!/usr/bin/env node
// Smoke test E2E de la API de Serenity. Recorre auth, contenido, proyectos, reseñas,
// valores, medios y solicitudes, y deja la base como la encontró.
//
//   node scripts/smoke.mjs                         # contra http://localhost:8081 (API directa)
//   SMOKE_BASE=http://localhost:8090 node scripts/smoke.mjs   # contra nginx (web) → /api y /media
//
// Credenciales: ADMIN_CORREO / ADMIN_PASSWORD del entorno o del archivo .env de la raíz.
// Si la API se corre sin nginx, define MEDIA_ROOT=http://localhost:9012/serenity-media para bajar las imágenes.
//
//   SMOKE_SOLO_PUBLICO=1 SMOKE_BASE=https://www.gruposerenity.com node scripts/smoke.mjs
// Solo lectura: corre los chequeos públicos (bloque 1) y termina. No necesita credenciales ni escribe nada:
// es el que usa scripts/deploy.sh contra producción.

import { readFileSync, existsSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const raiz = join(dirname(fileURLToPath(import.meta.url)), '..')
const env = { ...process.env }
if (existsSync(join(raiz, '.env'))) {
  for (const linea of readFileSync(join(raiz, '.env'), 'utf8').split(/\r?\n/)) {
    const m = linea.match(/^([A-Z0-9_]+)=(.*)$/)
    if (m && env[m[1]] === undefined) env[m[1]] = m[2]
  }
}

const BASE = (env.SMOKE_BASE ?? 'http://localhost:8081').replace(/\/$/, '')
const MEDIA_ROOT = env.MEDIA_ROOT
const SOLO_PUBLICO = env.SMOKE_SOLO_PUBLICO === '1'
const CORREO = env.ADMIN_CORREO
const PASSWORD = env.ADMIN_PASSWORD
if (!SOLO_PUBLICO && (!CORREO || !PASSWORD)) throw new Error('Falta ADMIN_CORREO / ADMIN_PASSWORD (env o .env).')

let pasadas = 0
const fallos = []
const ok = (nombre, cond, detalle = '') => {
  if (cond) {
    pasadas++
    console.log(`  ✓ ${nombre}`)
  } else {
    fallos.push(nombre)
    console.log(`  ✗ ${nombre} ${detalle}`)
  }
}
const seccion = (t) => console.log(`\n${t}`)
const dormir = (ms) => new Promise((r) => setTimeout(r, ms))

let token = null
async function api(method, ruta, { body, auth = true, form, raw } = {}) {
  const headers = {}
  if (auth && token) headers.Authorization = `Bearer ${token}`
  let payload
  if (form) payload = form
  else if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    payload = JSON.stringify(body)
  }
  const res = await fetch(BASE + ruta, { method, headers, body: payload })
  if (raw) return res
  const texto = await res.text()
  let json = null
  try {
    json = texto ? JSON.parse(texto) : null
  } catch {
    /* no JSON */
  }
  return { status: res.status, json, texto }
}

// PNG 1x1 válido, y variantes para las pruebas negativas.
const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==',
  'base64',
)
const subirArchivo = (bytes, nombre, tipo = 'image/png') => {
  const form = new FormData()
  form.append('archivo', new Blob([bytes], { type: tipo }), nombre)
  return api('POST', '/api/admin/media', { form })
}

async function postSolicitud(cuerpo, { reintentar = true } = {}) {
  for (let i = 0; i < 3; i++) {
    const r = await api('POST', '/api/public/solicitudes', { body: cuerpo, auth: false })
    if (r.status !== 429 || !reintentar) return r
    console.log('    (rate limit alcanzado, esperando 62 s a que abra la ventana...)')
    await dormir(62_000)
  }
  throw new Error('Rate limit persistente')
}

// Acceso directo a la base (para preparar casos que el API no permite, como vencer una retención).
// Usuario y base los toma del propio contenedor (POSTGRES_USER / POSTGRES_DB); el SQL viaja como argumento, sin comillas de shell.
const { execFileSync } = await import('node:child_process')
const psql = (sql) =>
  execFileSync('docker', ['exec', 'serenity_postgres', 'sh', '-c', 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc "$0"', sql], {
    stdio: ['ignore', 'pipe', 'pipe'],
  })
    .toString()
    .trim()
let dockerOk = false
try {
  dockerOk = psql('SELECT 1') === '1'
} catch {
  /* sin docker: se omiten los casos que necesitan la base */
}

const manana = () => new Date(Date.now() + 2 * 86400_000).toISOString().slice(0, 10)
const ayer = () => new Date(Date.now() - 2 * 86400_000).toISOString().slice(0, 10)

// ─── 1. Salud y contenido público ───────────────────────────────────────────
seccion('Salud y contenido público')
{
  const h = await fetch(BASE + '/health')
  ok('GET /health → 200', h.status === 200)

  const r = await api('GET', '/api/public/home', { auth: false })
  ok('GET /api/public/home → 200', r.status === 200)
  const claves = ['marca', 'navegacion', 'anuncio', 'hero', 'intro', 'valores', 'destinos', 'resenas', 'reservas', 'footer']
  ok('trae las 10 secciones', claves.every((c) => r.json?.contenido?.[c]))
  ok('logo expandido con url', typeof r.json?.contenido?.marca?.logo?.url === 'string')
  ok('no expone ids internos (logoMediaId)', r.json?.contenido?.marca?.logoMediaId === undefined)
  ok('hay proyectos, valores y reseñas', r.json?.proyectos?.length > 0 && r.json?.valores?.length > 0 && r.json?.resenas?.length > 0)
  ok('Cache-Control no-cache', (await api('GET', '/api/public/home', { auth: false, raw: true })).headers.get('cache-control') === 'no-cache')

  const url = r.json.proyectos[0].imagenes[0].url
  const img = await fetch(MEDIA_ROOT ? MEDIA_ROOT + url.replace(/^\/media/, '') : BASE + url)
  ok('las imágenes públicas se descargan (200 + image/*)', img.status === 200 && (img.headers.get('content-type') ?? '').startsWith('image/'))

  // El sitio no depende del WordPress anterior: ni enlaces a la tienda /shop/, ni "sitio actual", ni wp-content.
  const texto = JSON.stringify(r.json)
  ok('sin enlace "sitio actual" en el pie', !('sitioActualUrl' in r.json.contenido.footer) && !('sitioActualTexto' in r.json.contenido.footer))
  ok('sin URLs del WordPress (wp-content, /shop/)', !/wp-content|wp-json|gruposerenity\.com\/shop/i.test(texto))
  ok('ningún proyecto enlaza a gruposerenity.com', r.json.proyectos.every((p) => !/gruposerenity\.com/i.test(p.urlReserva ?? '')))
  ok('trae reservaDirecta (pagoEnLinea + alojamientos)', typeof r.json.reservaDirecta?.pagoEnLinea === 'boolean' && Array.isArray(r.json.reservaDirecta?.alojamientos))
  ok('reservaDirecta no expone el token iCal', !/tokenIcal|urlIcal/i.test(JSON.stringify(r.json.reservaDirecta)))

  // A través de nginx: la SPA responde en / y en sus rutas (fallback a index.html), sin restos del WordPress.
  if (BASE !== 'http://localhost:8081') {
    for (const ruta of ['/', '/reserva/SER-NOEXISTE']) {
      const p = await fetch(BASE + ruta)
      const html = await p.text()
      ok(`GET ${ruta} → SPA (200 html)`, p.status === 200 && html.includes('<div id="root">'), `status ${p.status}`)
      ok(`GET ${ruta} sin wp-content`, !/wp-content|wp-includes/i.test(html))
    }
  }
}

if (SOLO_PUBLICO) {
  console.log(`
${pasadas} pruebas OK, ${fallos.length} fallidas (solo chequeos públicos).`)
  process.exit(fallos.length ? 1 : 0)
}

// ─── 2. Autenticación ───────────────────────────────────────────────────────
seccion('Autenticación')
let refresh
let usuario
{
  ok('admin sin token → 401', (await api('GET', '/api/admin/dashboard', { auth: false })).status === 401)
  ok('token basura → 401', (await fetch(BASE + '/api/admin/dashboard', { headers: { Authorization: 'Bearer abc.def.ghi' } })).status === 401)
  ok('login con password mala → 401', (await api('POST', '/api/auth/login', { body: { correo: CORREO, password: 'incorrecta-123' }, auth: false })).status === 401)
  ok('login con correo inexistente → 401', (await api('POST', '/api/auth/login', { body: { correo: 'nadie@nada.com', password: 'x' }, auth: false })).status === 401)

  const l = await api('POST', '/api/auth/login', { body: { correo: CORREO, password: PASSWORD }, auth: false })
  ok('login correcto → 200 con user + tokens', l.status === 200 && l.json?.tokens?.accessToken && l.json?.user?.correo === CORREO.toLowerCase(), l.texto)
  token = l.json.tokens.accessToken
  refresh = l.json.tokens.refreshToken
  usuario = l.json.user

  ok('GET /api/auth/me → mismo usuario', (await api('GET', '/api/auth/me')).json?.id === usuario.id)

  const rf = await api('POST', '/api/auth/refresh', { body: { refreshToken: refresh }, auth: false })
  ok('refresh → nuevos tokens', rf.status === 200 && rf.json?.refreshToken && rf.json.refreshToken !== refresh)
  ok('reusar el refresh anterior → 401 (rotación)', (await api('POST', '/api/auth/refresh', { body: { refreshToken: refresh }, auth: false })).status === 401)
  refresh = rf.json.refreshToken
  token = rf.json.accessToken
}

// ─── 3. Medios ──────────────────────────────────────────────────────────────
seccion('Medios')
let mediaA, mediaB
{
  const a = await subirArchivo(PNG, 'smoke-a.png')
  ok('subir PNG válido → 201', a.status === 201 && a.json?.id, a.texto)
  mediaA = a.json
  const b = await subirArchivo(PNG, 'smoke-b.png')
  mediaB = b.json
  const pub = await fetch(MEDIA_ROOT ? MEDIA_ROOT + mediaA.url.replace(/^\/media/, '') : BASE + mediaA.url)
  ok('la imagen subida se descarga idéntica', pub.status === 200 && Buffer.from(await pub.arrayBuffer()).equals(PNG))

  ok('texto renombrado a .jpg → 400', (await subirArchivo(Buffer.from('esto no es una imagen, es texto'), 'fake.jpg', 'image/jpeg')).status === 400)
  ok('SVG → 400', (await subirArchivo(Buffer.from('<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>'), 'x.svg', 'image/svg+xml')).status === 400)
  const grande = Buffer.concat([PNG, Buffer.alloc(10 * 1024 * 1024)])
  const g = await subirArchivo(grande, 'grande.png')
  ok('imagen > 10 MB → 400/413', g.status === 400 || g.status === 413, String(g.status))
  ok('sin archivo → 400', (await api('POST', '/api/admin/media', { form: new FormData() })).status === 400)

  const lista = await api('GET', '/api/admin/media?page=1&pageSize=5')
  ok('listar medios paginado', lista.status === 200 && lista.json?.items?.length > 0 && lista.json.totalCount >= 2)
}

// ─── 4. Proyectos ───────────────────────────────────────────────────────────
seccion('Proyectos')
let p1, p2
{
  const datos = {
    nombre: 'Smoke Proyecto', etiqueta: 'Test', icono: 'sun', ubicacionEtiqueta: 'Ciudad · Depto',
    titulo: 'Título de prueba.', descripcion: 'Descripción de prueba.', metaLugar: 'Lugar', metaEstancia: '2 noches',
    urlReserva: 'https://example.com/reservar', ctaTexto: 'Ver', publicado: true,
  }
  const c = await api('POST', '/api/admin/proyectos', { body: datos })
  ok('crear proyecto → 201 con slug', c.status === 201 && c.json?.slug === 'smoke-proyecto', c.texto)
  p1 = c.json
  const c2 = await api('POST', '/api/admin/proyectos', { body: datos })
  ok('mismo nombre → slug distinto (-2)', c2.json?.slug === 'smoke-proyecto-2')
  p2 = c2.json

  ok('icono inválido → 400', (await api('POST', '/api/admin/proyectos', { body: { ...datos, icono: 'no-existe' } })).status === 400)
  ok('url javascript: → 400', (await api('POST', '/api/admin/proyectos', { body: { ...datos, urlReserva: 'javascript:alert(1)' } })).status === 400)
  ok('nombre vacío → 400', (await api('POST', '/api/admin/proyectos', { body: { ...datos, nombre: '' } })).status === 400)

  const i1 = await api('POST', `/api/admin/proyectos/${p1.id}/imagenes`, { body: { mediaId: mediaA.id, textoAlt: 'alt A' } })
  ok('agregar imagen 1', i1.status === 201 && i1.json?.imagenes?.length === 1, i1.texto)
  const i2 = await api('POST', `/api/admin/proyectos/${p1.id}/imagenes`, { body: { mediaId: mediaB.id, textoAlt: 'alt B' } })
  ok('agregar imagen 2', i2.json?.imagenes?.length === 2)
  ok('imagen inexistente → 400', (await api('POST', `/api/admin/proyectos/${p1.id}/imagenes`, { body: { mediaId: 999999, textoAlt: '' } })).status === 400)
  const [img1, img2] = i2.json.imagenes

  const ord = await api('PUT', `/api/admin/proyectos/${p1.id}/imagenes/orden`, { body: { ids: [img2.id, img1.id] } })
  ok('reordenar imágenes → 204', ord.status === 204)
  ok('reordenar con ids incompletos → 400', (await api('PUT', `/api/admin/proyectos/${p1.id}/imagenes/orden`, { body: { ids: [img1.id] } })).status === 400)
  ok('editar alt → 204', (await api('PUT', `/api/admin/proyectos/${p1.id}/imagenes/${img1.id}`, { body: { textoAlt: 'nuevo alt' } })).status === 204)
  const det = await api('GET', `/api/admin/proyectos/${p1.id}`)
  ok('orden y alt persistidos', det.json?.imagenes?.[0]?.id === img2.id && det.json.imagenes[1].textoAlt === 'nuevo alt')

  const upd = await api('PUT', `/api/admin/proyectos/${p1.id}`, { body: { ...datos, titulo: 'Editado.' } })
  ok('actualizar proyecto (slug intacto)', upd.status === 200 && upd.json?.titulo === 'Editado.' && upd.json.slug === 'smoke-proyecto')

  let home = (await api('GET', '/api/public/home', { auth: false })).json
  ok('proyecto publicado aparece en home', home.proyectos.some((p) => p.id === p1.id))

  const ordP = await api('PUT', '/api/admin/proyectos/orden', { body: { ids: [p2.id, ...(await api('GET', '/api/admin/proyectos')).json.map((p) => p.id).filter((i) => i !== p2.id)] } })
  ok('reordenar proyectos → 204', ordP.status === 204)
  home = (await api('GET', '/api/public/home', { auth: false })).json
  ok('el orden se refleja en home', home.proyectos[0].id === p2.id)
  ok('ids de proyectos incompletos → 400', (await api('PUT', '/api/admin/proyectos/orden', { body: { ids: [p2.id] } })).status === 400)

  await api('PUT', `/api/admin/proyectos/${p2.id}`, { body: { ...datos, publicado: false } })
  home = (await api('GET', '/api/public/home', { auth: false })).json
  ok('despublicado NO aparece en home', !home.proyectos.some((p) => p.id === p2.id))
  ok('proyecto inexistente → 404', (await api('GET', '/api/admin/proyectos/999999')).status === 404)

  ok('borrar media en uso → 409', (await api('DELETE', `/api/admin/media/${mediaA.id}`)).status === 409)
  ok('quitar imagen del proyecto → 204', (await api('DELETE', `/api/admin/proyectos/${p1.id}/imagenes/${img1.id}`)).status === 204)
}

// ─── 5. Reseñas y valores ───────────────────────────────────────────────────
seccion('Reseñas')
{
  const c = await api('POST', '/api/admin/resenas', { body: { autor: 'Smoke', contexto: 'Test', texto: 'Muy bien', calificacion: 4, publicada: true } })
  ok('crear reseña → 201', c.status === 201 && c.json?.id, c.texto)
  ok('calificación 6 → 400', (await api('POST', '/api/admin/resenas', { body: { autor: 'x', texto: 'y', calificacion: 6 } })).status === 400)
  ok('actualizar reseña', (await api('PUT', `/api/admin/resenas/${c.json.id}`, { body: { autor: 'Smoke2', contexto: 'Test', texto: 'Muy bien', calificacion: 5, publicada: false } })).json?.autor === 'Smoke2')
  let home = (await api('GET', '/api/public/home', { auth: false })).json
  ok('reseña despublicada oculta en home', !home.resenas.some((r) => r.id === c.json.id))
  await api('PUT', `/api/admin/resenas/${c.json.id}`, { body: { autor: 'Smoke2', contexto: 'Test', texto: 'Muy bien', calificacion: 5, publicada: true } })
  home = (await api('GET', '/api/public/home', { auth: false })).json
  ok('reseña publicada visible en home', home.resenas.some((r) => r.id === c.json.id))
  const ids = (await api('GET', '/api/admin/resenas')).json.map((r) => r.id).reverse()
  ok('reordenar reseñas → 204', (await api('PUT', '/api/admin/resenas/orden', { body: { ids } })).status === 204)
  ok('borrar reseña → 204', (await api('DELETE', `/api/admin/resenas/${c.json.id}`)).status === 204)
  ok('borrar de nuevo → 404', (await api('DELETE', `/api/admin/resenas/${c.json.id}`)).status === 404)
  // Restaurar el orden original.
  await api('PUT', '/api/admin/resenas/orden', { body: { ids: ids.filter((i) => i !== c.json.id).reverse() } })
}

seccion('Valores')
{
  const v = { titulo: 'Smoke valor', descripcion: 'Descripción', icono: 'star', variante: 'ancha', activo: true }
  const c = await api('POST', '/api/admin/valores', { body: v })
  ok('crear valor → 201', c.status === 201 && c.json?.id, c.texto)
  ok('variante inválida → 400', (await api('POST', '/api/admin/valores', { body: { ...v, variante: 'gigante' } })).status === 400)
  ok('icono inválido → 400', (await api('POST', '/api/admin/valores', { body: { ...v, icono: 'zzz' } })).status === 400)
  await api('PUT', `/api/admin/valores/${c.json.id}`, { body: { ...v, activo: false } })
  const home = (await api('GET', '/api/public/home', { auth: false })).json
  ok('valor inactivo oculto en home', !home.valores.some((x) => x.id === c.json.id))
  ok('borrar valor → 204', (await api('DELETE', `/api/admin/valores/${c.json.id}`)).status === 204)
}

// ─── 6. Contenido del sitio ─────────────────────────────────────────────────
seccion('Contenido del sitio')
{
  const antes = (await api('GET', '/api/admin/contenido')).json
  ok('GET contenido admin trae 10 secciones', Object.keys(antes).length === 10)
  ok('admin ve logoMediaId y logo expandido', antes.marca.logoMediaId && antes.marca.logo?.url)

  const hero = { ...antes.hero, kicker: 'KICKER-SMOKE' }
  const put = await api('PUT', '/api/admin/contenido/hero', { body: hero })
  ok('PUT hero → 200', put.status === 200 && put.json?.kicker === 'KICKER-SMOKE', put.texto)
  let home = (await api('GET', '/api/public/home', { auth: false })).json
  ok('el cambio se refleja en home', home.contenido.hero.kicker === 'KICKER-SMOKE')
  ok('las imágenes de la sección se conservan', home.contenido.hero.imagenPrincipal?.url)

  ok('sección desconocida → 404', (await api('PUT', '/api/admin/contenido/nada', { body: {} })).status === 404)
  ok('href javascript: → 400', (await api('PUT', '/api/admin/contenido/hero', { body: { ...hero, ctaPrimarioHref: 'javascript:alert(1)' } })).status === 400)
  ok('mediaId inexistente → 400', (await api('PUT', '/api/admin/contenido/hero', { body: { ...hero, imagenPrincipalMediaId: 999999 } })).status === 400)
  ok('kicker vacío → 400', (await api('PUT', '/api/admin/contenido/hero', { body: { ...hero, kicker: '' } })).status === 400)
  ok('navegación vacía → 400', (await api('PUT', '/api/admin/contenido/navegacion', { body: { items: [], ctaTexto: 'x', ctaHref: '#a' } })).status === 400)

  await api('PUT', '/api/admin/contenido/hero', { body: antes.hero })
  home = (await api('GET', '/api/public/home', { auth: false })).json
  ok('restaurado', home.contenido.hero.kicker === antes.hero.kicker)
}

// ─── 7. Solicitudes ─────────────────────────────────────────────────────────
seccion('Solicitudes')
{
  const proyectoPublicado = (await api('GET', '/api/public/home', { auth: false })).json.proyectos[0]
  const base = { nombre: 'Smoke Huésped', contacto: 'smoke@example.com', fechaLlegada: manana(), huespedes: 2, proyectoId: proyectoPublicado.id }
  const c = await postSolicitud(base)
  ok('solicitud válida → 201', c.status === 201 && c.json?.id, c.texto)
  ok('fecha pasada → 400', (await postSolicitud({ ...base, fechaLlegada: ayer() })).status === 400)
  ok('contacto inválido → 400', (await postSolicitud({ ...base, contacto: 'no-es-contacto' })).status === 400)
  ok('sin destino → 400', (await postSolicitud({ ...base, proyectoId: null })).status === 400)
  ok('proyecto inexistente → 400', (await postSolicitud({ ...base, proyectoId: 999999 })).status === 400)
  const w = await postSolicitud({ ...base, nombre: 'Smoke Honeypot', sitioWeb: 'http://spam.example' })
  ok('honeypot lleno → 202', w.status === 202)

  const lista = await api('GET', '/api/admin/solicitudes?q=Smoke&pageSize=50')
  ok('admin ve la solicitud como "nueva"', lista.json?.items?.some((s) => s.id === c.json.id && s.estado === 'nueva'))
  ok('el honeypot NO se persistió', !lista.json?.items?.some((s) => s.nombre === 'Smoke Honeypot'))
  ok('destino se toma del proyecto', lista.json.items.find((s) => s.id === c.json.id)?.destinoTexto === proyectoPublicado.nombre)
  ok('filtro por estado', (await api('GET', '/api/admin/solicitudes?estado=nueva')).json?.items?.every((s) => s.estado === 'nueva'))
  ok('estado inválido en filtro → 400', (await api('GET', '/api/admin/solicitudes?estado=zzz')).status === 400)
  ok('búsqueda con comodín literal no explota', (await api('GET', '/api/admin/solicitudes?q=%25_%5C')).status === 200)

  const up = await api('PUT', `/api/admin/solicitudes/${c.json.id}`, { body: { estado: 'contactada', notas: 'Llamé por WhatsApp' } })
  ok('cambiar estado + notas', up.status === 200 && up.json?.estado === 'contactada' && up.json.notas === 'Llamé por WhatsApp', up.texto)
  ok('estado inválido → 400', (await api('PUT', `/api/admin/solicitudes/${c.json.id}`, { body: { estado: 'zzz' } })).status === 400)
  const dash = (await api('GET', '/api/admin/dashboard')).json
  ok('dashboard con contadores', dash?.solicitudesTotal >= 1 && dash.proyectosTotal >= 2)
  ok('borrar solicitud → 204', (await api('DELETE', `/api/admin/solicitudes/${c.json.id}`)).status === 204)
  ok('solicitud borrada → 404', (await api('GET', `/api/admin/solicitudes/${c.json.id}`)).status === 404)

  // Rate limit: ráfaga por encima de 5/min desde la misma IP.
  const rafaga = []
  for (let i = 0; i < 8; i++) rafaga.push((await postSolicitud({ ...base, fechaLlegada: ayer() }, { reintentar: false })).status)
  ok('rate limit: la ráfaga produce 429', rafaga.includes(429), rafaga.join(','))
}

// ─── 7b. Anuncios de Airbnb y calendario (iCal) ─────────────────────────────
seccion('Anuncios de Airbnb y sincronización de calendario')
{
  const proyectos = (await api('GET', '/api/admin/proyectos')).json
  const proyecto = proyectos.find((p) => p.publicado) ?? proyectos[0]
  const base = `/api/admin/proyectos/${proyecto.id}/anuncios`
  const ROOM = '900000000000000001'
  const urlAnuncio = `https://www.airbnb.com.co/rooms/${ROOM}?source_impression_id=p3_x`
  const cuerpo = (extra = {}) => ({ nombre: 'Anuncio smoke', urlAnuncio, ...extra })

  ok('sin token → 401', (await api('GET', base, { auth: false })).status === 401)
  ok('proyecto inexistente → 404', (await api('GET', '/api/admin/proyectos/999999/anuncios')).status === 404)
  ok('URL que no es de Airbnb → 400', (await api('POST', base, { body: cuerpo({ urlAnuncio: 'https://example.com/rooms/1' }) })).status === 400)
  ok('iCal de un host ajeno (SSRF) → 400', (await api('POST', base, { body: cuerpo({ icalUrl: 'https://interno.local/a.ics' }) })).status === 400)
  ok('iCal sin https → 400', (await api('POST', base, { body: cuerpo({ icalUrl: `http://www.airbnb.com/calendar/ical/${ROOM}.ics` }) })).status === 400)
  ok('iCal de otro anuncio (id distinto) → 400', (await api('POST', base, { body: cuerpo({ icalUrl: 'https://www.airbnb.com/calendar/ical/123.ics?s=x' }) })).status === 400)

  const sinIcal = await api('POST', base, { body: cuerpo() })
  ok('crear anuncio sin iCal → 201', sinIcal.status === 201 && sinIcal.json.airbnbRoomId === ROOM)
  ok('sin iCal no hay estado de sincronización', sinIcal.json?.ultimaSyncOk === null && sinIcal.json?.ultimaSyncError === null)
  ok('mismo anuncio otra vez → 409', (await api('POST', base, { body: cuerpo({ nombre: 'Otro' }) })).status === 409)
  ok('sincronizar sin iCal → 400', (await api('POST', `/api/admin/anuncios/${sinIcal.json.id}/sincronizar`)).status === 400)
  ok('sincronizar inexistente → 404', (await api('POST', '/api/admin/anuncios/999999/sincronizar')).status === 404)
  ok('el listado incluye el anuncio', (await api('GET', base)).json.some((a) => a.id === sinIcal.json.id))

  // Con un enlace de Airbnb que no existe la descarga falla; el anuncio se guarda igual y registra el motivo.
  const icalFalso = `https://www.airbnb.com/calendar/ical/${ROOM}.ics?s=secreto-falso`
  const conFallo = await api('PUT', `/api/admin/anuncios/${sinIcal.json.id}`, { body: cuerpo({ icalUrl: icalFalso }) })
  ok('guardar iCal inservible → 200 con error registrado', conFallo.status === 200 && !!conFallo.json.ultimaSyncError && conFallo.json.ultimaSyncOk === null)
  ok('el error no filtra la URL con el secreto', !JSON.stringify(conFallo.json.ultimaSyncError).includes('secreto-falso'))

  // Disponibilidad pública
  const dispUrl = `/api/public/proyectos/${proyecto.slug}/disponibilidad`
  const d0 = await api('GET', dispUrl, { auth: false })
  ok('disponibilidad pública → 200 y sin datos secretos', d0.status === 200 && !d0.texto.includes('icalUrl') && !d0.texto.includes('secreto-falso') && !d0.texto.includes(ROOM + '.ics'))
  ok('anuncio sin sincronizar: no se afirma ocupación del proyecto', d0.json.ocupadoProyecto.length === 0 && d0.json.anuncios.some((a) => a.id === sinIcal.json.id && a.sincronizado === false))
  ok('slug inexistente → 404', (await api('GET', '/api/public/proyectos/no-existe/disponibilidad', { auth: false })).status === 404)
  ok('rango invertido → 400', (await api('GET', `${dispUrl}?desde=2027-02-01&hasta=2027-01-01`, { auth: false })).status === 400)
  ok('rango de más de 400 días → 400', (await api('GET', `${dispUrl}?desde=2027-01-01&hasta=2029-01-01`, { auth: false })).status === 400)

  // Camino feliz: hace falta un iCal servible y poder reapuntar el anuncio (el API solo acepta hosts de Airbnb).
  const { createServer } = await import('node:http')

  if (!dockerOk) {
    console.log('  (sin acceso a docker/postgres: se omite la sincronización real)')
  } else {
    const ics = (eventos) => `BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Airbnb Inc//Hosting Calendar 0.8.8//EN\r\n${eventos}END:VCALENDAR\r\n`
    const ev = (uid, a, b) => `BEGIN:VEVENT\r\nDTEND;VALUE=DATE:${b}\r\nDTSTART;VALUE=DATE:${a}\r\nUID:${uid}\r\nSUMMARY:Reserved\r\nEND:VEVENT\r\n`
    let contenido = ics(ev('r1@airbnb.com', '20270110', '20270115') + ev('r2@airbnb.com', '20270128', '20270202'))
    let estado = 200
    const servidor = createServer((_, res) => {
      res.writeHead(estado, { 'Content-Type': 'text/calendar' })
      res.end(contenido)
    })
    await new Promise((r) => servidor.listen(9555, '0.0.0.0', r))
    const id = sinIcal.json.id
    const apuntar = (url) => psql(`UPDATE anuncios SET ical_url='${url}' WHERE id=${id}`)
    const rango = '?desde=2027-01-01&hasta=2027-03-01'
    const ocupado = async () => (await api('GET', dispUrl + rango, { auth: false })).json.anuncios.find((a) => a.id === id)
    try {
      apuntar('http://host.docker.internal:9555/cal.ics')
      const s1 = await api('POST', `/api/admin/anuncios/${id}/sincronizar`)
      ok('sincronizar → 200, sin error y con fecha de éxito', s1.status === 200 && s1.json.ultimaSyncError === null && !!s1.json.ultimaSyncOk)

      const d1 = (await api('GET', dispUrl + rango, { auth: false })).json
      const a1 = d1.anuncios.find((a) => a.id === id)
      const esperado = [
        { desde: '2027-01-10', hasta: '2027-01-15' },
        { desde: '2027-01-28', hasta: '2027-02-02' },
      ]
      ok('noches ocupadas = [10-15 ene) y [28 ene-2 feb)', JSON.stringify(a1.ocupado) === JSON.stringify(esperado), JSON.stringify(a1.ocupado))
      ok('el día de salida (15 ene) no está ocupado', !a1.ocupado.some((r) => r.desde <= '2027-01-15' && '2027-01-15' < r.hasta))
      const sinSync = d1.anuncios.filter((a) => a.id !== id && !a.sincronizado).length
      ok('proyecto ocupado solo si todos sus anuncios están sincronizados', sinSync > 0 ? d1.ocupadoProyecto.length === 0 : d1.ocupadoProyecto.length === 2)

      // Refresco bajo demanda: con el último intento viejo, consultar la disponibilidad vuelve a descargar.
      contenido = ics(ev('r3@airbnb.com', '20270120', '20270122'))
      psql(`UPDATE anuncios SET ultima_sync_intento = now() - interval '10 minutes' WHERE id=${id}`)
      const a2 = await ocupado()
      ok('consultar con caché vencida refresca el calendario', JSON.stringify(a2.ocupado) === JSON.stringify([{ desde: '2027-01-20', hasta: '2027-01-22' }]), JSON.stringify(a2.ocupado))

      // Dentro de la ventana no se vuelve a descargar (una sola descarga para muchas visitas).
      contenido = ics(ev('r4@airbnb.com', '20270301', '20270303'))
      const a3 = await ocupado()
      ok('dentro de la ventana no vuelve a descargar', a3.ocupado.length === 1 && a3.ocupado[0].desde === '2027-01-20')

      // Un fallo del servidor del calendario conserva los bloqueos previos y registra el error.
      estado = 503
      const s2 = await api('POST', `/api/admin/anuncios/${id}/sincronizar`)
      ok('fallo de descarga → error registrado', s2.status === 200 && /503/.test(s2.json.ultimaSyncError ?? ''))
      const a4 = await ocupado()
      ok('un fallo conserva los bloqueos anteriores', a4.ocupado.length === 1 && a4.ocupado[0].desde === '2027-01-20')

      // Una página HTML (login, error) no es un iCal: no debe vaciar el calendario.
      estado = 200
      contenido = '<html>Inicia sesión</html>'
      const s3 = await api('POST', `/api/admin/anuncios/${id}/sincronizar`)
      ok('respuesta que no es iCal → error, no vacía el calendario', !!s3.json.ultimaSyncError && (await ocupado()).ocupado.length === 1)

      // Cambiar o quitar el enlace descarta los bloqueos del calendario anterior.
      const quitado = await api('PUT', `/api/admin/anuncios/${id}`, { body: cuerpo({ icalUrl: '' }) })
      ok('quitar el iCal → 200 y sin estado de sincronización', quitado.status === 200 && quitado.json.icalUrl === null && quitado.json.ultimaSyncOk === null)
      const a5 = await ocupado()
      ok('quitar el iCal descarta sus bloqueos', a5.ocupado.length === 0 && a5.sincronizado === false)
    } finally {
      servidor.close()
    }
  }

  ok('borrar anuncio → 204', (await api('DELETE', `/api/admin/anuncios/${sinIcal.json.id}`)).status === 204)
  ok('borrar anuncio otra vez → 404', (await api('DELETE', `/api/admin/anuncios/${sinIcal.json.id}`)).status === 404)
  ok('ya no aparece en la disponibilidad pública', !(await api('GET', dispUrl, { auth: false })).json.anuncios.some((a) => a.id === sinIcal.json.id))
}

// ─── 7c. Reserva directa: alojamientos, cotización, retención y pagos ───────
seccion('Reserva directa (alojamientos, reservas y pagos con Wompi)')
{
  const { createHash } = await import('node:crypto')
  const SECRETO_EVENTOS = env.WOMPI_SECRETO_EVENTOS

  // Crear reservas tiene rate limit (5/min por IP): si se agota, espera a que abra la ventana.
  const postReserva = async (cuerpo) => {
    for (let i = 0; i < 3; i++) {
      const r = await api('POST', '/api/public/reservas', { body: cuerpo, auth: false })
      if (r.status !== 429) return r
      console.log('    (rate limit de reservas, esperando 62 s...)')
      await dormir(62_000)
    }
    throw new Error('Rate limit persistente')
  }
  const evento = (tx, { secreto = SECRETO_EVENTOS, tipo = 'transaction.updated' } = {}) => {
    const timestamp = Math.floor(Date.now() / 1000)
    const checksum = createHash('sha256').update(`${tx.id}${tx.status}${tx.amount_in_cents}${timestamp}${secreto}`).digest('hex')
    return {
      event: tipo,
      data: { transaction: { currency: 'COP', payment_method_type: 'CARD', ...tx } },
      environment: 'test',
      signature: { properties: ['transaction.id', 'transaction.status', 'transaction.amount_in_cents'], checksum },
      timestamp,
      sent_at: new Date().toISOString(),
    }
  }
  const enviarEvento = (tx, opciones) => api('POST', '/api/public/pagos/wompi/eventos', { body: evento(tx, opciones), auth: false })
  const consultar = (r) => api('GET', `/api/public/reservas/${r.codigo}?t=${r.token}`, { auth: false })

  const pr = (
    await api('POST', '/api/admin/proyectos', {
      body: {
        nombre: 'Smoke Reservas', etiqueta: 'Test', icono: 'home', ubicacionEtiqueta: 'Lugar', titulo: 'T.', descripcion: 'D.',
        metaLugar: '', metaEstancia: '', urlReserva: null, ctaTexto: 'Ver', publicado: true,
      },
    })
  ).json
  const baseAloj = `/api/admin/proyectos/${pr.id}/alojamientos`
  const datosAloj = {
    nombre: 'Cabaña smoke', descripcion: 'Con vista al lago.', imagenMediaId: mediaA.id, capacidad: 4, huespedesIncluidos: 2,
    precioNoche: 300000, recargoHuespedExtra: 50000, tarifaAseo: 80000, estanciaMinima: 2, anuncioId: null, publicado: true,
  }

  ok('alojamientos sin token → 401', (await api('GET', baseAloj, { auth: false })).status === 401)
  ok('incluidos > capacidad → 400', (await api('POST', baseAloj, { body: { ...datosAloj, huespedesIncluidos: 6 } })).status === 400)
  ok('precio 0 → 400', (await api('POST', baseAloj, { body: { ...datosAloj, precioNoche: 0 } })).status === 400)
  ok('imagen inexistente → 400', (await api('POST', baseAloj, { body: { ...datosAloj, imagenMediaId: 999999 } })).status === 400)
  ok('anuncio inexistente o de otro proyecto → 400', (await api('POST', baseAloj, { body: { ...datosAloj, anuncioId: 999999 } })).status === 400)

  const ca = await api('POST', baseAloj, { body: datosAloj })
  ok('crear alojamiento → 201 con imagen y enlace iCal', ca.status === 201 && ca.json?.imagen?.url && /\/api\/public\/ical\/[0-9a-f]{48}\.ics$/.test(ca.json.urlIcal), ca.texto)
  const aloj = ca.json
  ok('la imagen del alojamiento queda en uso (borrar → 409)', (await api('DELETE', `/api/admin/media/${mediaA.id}`)).status === 409)

  // Temporadas
  const tarifas = `/api/admin/alojamientos/${aloj.id}/tarifas`
  const t1 = await api('POST', tarifas, { body: { nombre: 'Temporada alta', desde: '2027-06-10', hasta: '2027-06-12', precioNoche: 450000 } })
  ok('crear temporada → 201', t1.status === 201 && t1.json.precioNoche === 450000)
  ok('temporada que se cruza → 409', (await api('POST', tarifas, { body: { nombre: 'Otra', desde: '2027-06-11', hasta: '2027-06-20', precioNoche: 1 } })).status === 409)
  ok('temporada invertida → 400', (await api('POST', tarifas, { body: { nombre: 'Mal', desde: '2027-07-10', hasta: '2027-07-01', precioNoche: 1 } })).status === 400)

  // Landing
  const home = (await api('GET', '/api/public/home', { auth: false })).json
  const PAGO = home.reservaDirecta?.pagoEnLinea === true
  const pub = home.reservaDirecta?.alojamientos?.find((a) => a.id === aloj.id)
  ok('la landing incluye el alojamiento publicado', !!pub && pub.proyectoId === pr.id && pub.precioNoche === 300000)
  ok('la landing no expone el token iCal', !JSON.stringify(home.reservaDirecta).includes('token') && !JSON.stringify(home).includes(aloj.urlIcal.split('/').pop()))
  console.log(`    (pago en línea ${PAGO ? 'ACTIVO' : 'apagado'} en este entorno)`)

  // Cotización: 8→13 jun, 4 huéspedes. Noches 8, 9 (base), 10, 11 (temporada), 12 (base).
  const cot = (b) => api('POST', '/api/public/reservas/cotizar', { body: { alojamientoId: aloj.id, ...b }, auth: false })
  const c1 = await cot({ llegada: '2027-06-08', salida: '2027-06-13', huespedes: 4 })
  ok('cotizar → 200, disponible', c1.status === 200 && c1.json.disponible === true, c1.texto)
  ok('cotización: 5 noches, temporada y recargo', c1.json?.cotizacion?.noches === 5 && c1.json.cotizacion.subtotal === 3 * 300000 + 2 * 450000 + 2 * 50000 * 5 && c1.json.cotizacion.total === c1.json.cotizacion.subtotal + 80000, JSON.stringify(c1.json?.cotizacion))
  ok('cotización: tramos base / temporada / base', c1.json?.cotizacion?.tramos?.map((t) => t.noches).join(',') === '2,2,1')
  ok('menos de la estancia mínima → 400', (await cot({ llegada: '2027-06-08', salida: '2027-06-09', huespedes: 2 })).status === 400)
  ok('más huéspedes que la capacidad → 400', (await cot({ llegada: '2027-06-08', salida: '2027-06-10', huespedes: 9 })).status === 400)
  ok('llegada en el pasado → 400', (await cot({ llegada: ayer(), salida: manana(), huespedes: 2 })).status === 400)
  ok('alojamiento inexistente → 404', (await api('POST', '/api/public/reservas/cotizar', { body: { alojamientoId: 999999, llegada: '2027-06-08', salida: '2027-06-10', huespedes: 2 }, auth: false })).status === 404)

  // Crear
  const reservar = (b) => postReserva({ alojamientoId: aloj.id, huespedes: 2, nombre: 'Huésped Smoke', correo: 'huesped@example.com', telefono: '+57 300 123 4567', ...b })
  ok('correo inválido → 400', (await reservar({ llegada: '2027-06-08', salida: '2027-06-13', correo: 'no-es-correo' })).status === 400)
  ok('honeypot → 202 sin crear', (await reservar({ llegada: '2027-06-08', salida: '2027-06-13', sitioWeb: 'http://spam' })).status === 202)
  const r1 = await reservar({ llegada: '2027-06-08', salida: '2027-06-13', huespedes: 4, nota: 'Llegamos tarde.' })
  ok('crear reserva → 201 con código y token', r1.status === 201 && /^SER-[2-9A-HJKMNP-Z]{6}$/.test(r1.json?.codigo) && r1.json.token?.length === 32, r1.texto)
  ok('el total cobrado es el de la cotización', r1.json?.total === c1.json.cotizacion.total)
  if (PAGO) {
    const q = new URL(r1.json.checkoutUrl).searchParams
    ok('checkout de Wompi: centavos, referencia -1 y regreso a la reserva', q.get('amount-in-cents') === String(r1.json.total * 100) && q.get('reference') === `${r1.json.codigo}-1` && q.get('redirect-url').includes(`/reserva/${r1.json.codigo}?t=${r1.json.token}`) && !!q.get('signature:integrity'))
  } else {
    ok('sin pago en línea no hay checkout', r1.json?.checkoutUrl === null)
  }
  ok('mismas fechas otra vez → 409', (await reservar({ llegada: '2027-06-08', salida: '2027-06-13' })).status === 409)
  ok('fechas que se cruzan → 409', (await reservar({ llegada: '2027-06-12', salida: '2027-06-15' })).status === 409)
  ok('llegar el día de salida de otra reserva sí se puede (cotizar)', (await cot({ llegada: '2027-06-13', salida: '2027-06-15', huespedes: 2 })).json?.disponible === true)
  ok('cotizar fechas retenidas → disponible: false', (await cot({ llegada: '2027-06-09', salida: '2027-06-11', huespedes: 2 })).json?.disponible === false)

  const disp = await api('GET', `/api/public/alojamientos/${aloj.id}/disponibilidad?desde=2027-06-01&hasta=2027-07-01`, { auth: false })
  ok('disponibilidad pública muestra la retención', disp.status === 200 && JSON.stringify(disp.json.ocupado) === JSON.stringify([{ desde: '2027-06-08', hasta: '2027-06-13' }]), disp.texto)

  // Consulta pública
  const g1 = await consultar(r1.json)
  ok('consultar con token → 200 pendiente de pago', g1.status === 200 && g1.json.estado === 'pendiente_pago' && g1.json.puedePagar === PAGO && !!g1.json.expiraEn)
  ok('consultar no expone correo ni teléfono', !g1.texto.includes('huesped@example.com') && !g1.texto.includes('300 123'))
  ok('token incorrecto → 404', (await api('GET', `/api/public/reservas/${r1.json.codigo}?t=${'0'.repeat(32)}`, { auth: false })).status === 404)
  ok('sin token → 404', (await api('GET', `/api/public/reservas/${r1.json.codigo}`, { auth: false })).status === 404)

  // Feed .ics de salida
  const rutaIcal = new URL(aloj.urlIcal).pathname
  const ical = await api('GET', rutaIcal, { auth: false, raw: true })
  const icsTexto = await ical.text()
  ok('feed .ics → 200 text/calendar con la reserva', ical.status === 200 && (ical.headers.get('content-type') ?? '').startsWith('text/calendar') && icsTexto.includes('DTSTART;VALUE=DATE:20270608') && icsTexto.includes('DTEND;VALUE=DATE:20270613'))
  ok('feed .ics sin datos del huésped', !icsTexto.includes('Huésped Smoke') && !icsTexto.includes('example.com'))
  ok('token de feed inválido → 404', (await api('GET', `/api/public/ical/${'a'.repeat(48)}.ics`, { auth: false })).status === 404)

  const reservasCreadas = [r1.json]

  if (PAGO && SECRETO_EVENTOS) {
    const total1 = r1.json.total * 100
    ok('evento con firma inválida → 401', (await enviarEvento({ id: 'tx-falsa', status: 'APPROVED', amount_in_cents: total1, reference: `${r1.json.codigo}-1` }, { secreto: 'otro' })).status === 401)
    ok('la reserva sigue pendiente tras el evento falso', (await consultar(r1.json)).json.estado === 'pendiente_pago')

    const rechazo = await enviarEvento({ id: 'tx-smoke-1', status: 'DECLINED', amount_in_cents: total1, reference: `${r1.json.codigo}-1` })
    const g2 = (await consultar(r1.json)).json
    ok('pago rechazado → 200; la reserva sigue retenida y se puede reintentar', rechazo.status === 200 && g2.estado === 'pendiente_pago' && g2.pagoEstado === 'DECLINED' && g2.puedePagar === true)
    const reintento = await api('POST', `/api/public/reservas/${r1.json.codigo}/pago?t=${r1.json.token}`, { auth: false })
    ok('reintentar pago → nueva referencia -2', reintento.status === 200 && new URL(reintento.json.checkoutUrl).searchParams.get('reference') === `${r1.json.codigo}-2`)

    const aprob = await enviarEvento({ id: 'tx-smoke-2', status: 'APPROVED', amount_in_cents: total1, reference: `${r1.json.codigo}-2`, payment_method_type: 'NEQUI' })
    const g3 = (await consultar(r1.json)).json
    ok('pago aprobado → reserva confirmada', aprob.status === 200 && g3.estado === 'confirmada' && g3.pagoEstado === 'APPROVED' && g3.expiraEn === null && g3.puedePagar === false, JSON.stringify(g3))
    ok('el mismo evento otra vez es idempotente', (await enviarEvento({ id: 'tx-smoke-2', status: 'APPROVED', amount_in_cents: total1, reference: `${r1.json.codigo}-2` })).status === 200 && (await consultar(r1.json)).json.estado === 'confirmada')
    ok('ya confirmada: pedir otro pago → 409', (await api('POST', `/api/public/reservas/${r1.json.codigo}/pago?t=${r1.json.token}`, { auth: false })).status === 409)
    ok('verificar con una transacción desconocida → 200 sin cambios', (await api('POST', `/api/public/reservas/${r1.json.codigo}/verificar?t=${r1.json.token}`, { body: { transaccionId: 'no-existe-123' }, auth: false })).json?.estado === 'confirmada')
    ok('evento con referencia desconocida → 200 (se ignora)', (await enviarEvento({ id: 'tx-x', status: 'APPROVED', amount_in_cents: 100, reference: 'SER-ZZZZZZ-1' })).status === 200)

    // Monto distinto → revisión (y las fechas se liberan para no bloquear el calendario por un error).
    const r2 = (await reservar({ llegada: '2027-06-20', salida: '2027-06-23' })).json
    reservasCreadas.push(r2)
    await enviarEvento({ id: 'tx-smoke-3', status: 'APPROVED', amount_in_cents: 100, reference: `${r2.codigo}-1` })
    ok('pago con monto distinto → revisión', (await consultar(r2)).json.estado === 'revision')

    if (dockerOk) {
      // Pago que llega después de vencer la retención, con las fechas libres → se confirma igual.
      const r3 = (await reservar({ llegada: '2027-06-25', salida: '2027-06-28' })).json
      reservasCreadas.push(r3)
      psql(`UPDATE reservas SET expira_en = now() - interval '1 minute' WHERE codigo = '${r3.codigo}'`)
      ok('retención vencida → expirada al consultar', (await consultar(r3)).json.estado === 'expirada')
      await enviarEvento({ id: 'tx-smoke-4', status: 'APPROVED', amount_in_cents: r3.total * 100, reference: `${r3.codigo}-1` })
      ok('pago tardío con fechas libres → confirmada', (await consultar(r3)).json.estado === 'confirmada')

      // Pago tardío con las fechas ya tomadas por otra reserva → revisión, sin doble reserva.
      const r5 = (await reservar({ llegada: '2027-07-01', salida: '2027-07-04' })).json
      reservasCreadas.push(r5)
      psql(`UPDATE reservas SET expira_en = now() - interval '1 minute' WHERE codigo = '${r5.codigo}'`)
      const r6 = await reservar({ llegada: '2027-07-01', salida: '2027-07-04' })
      ok('vencida la retención, otro huésped puede tomar las fechas', r6.status === 201)
      reservasCreadas.push(r6.json)
      await enviarEvento({ id: 'tx-smoke-5', status: 'APPROVED', amount_in_cents: r5.total * 100, reference: `${r5.codigo}-1` })
      ok('pago tardío con fechas tomadas → revisión (no hay doble reserva)', (await consultar(r5)).json.estado === 'revision' && (await consultar(r6.json)).json.estado === 'pendiente_pago')
    }
  } else {
    console.log('  (pago en línea apagado o sin WOMPI_SECRETO_EVENTOS: se omiten los eventos de Wompi)')
  }

  // Admin
  const lista = await api('GET', `/api/admin/reservas?q=${r1.json.codigo}`)
  ok('admin: buscar por código', lista.status === 200 && lista.json.items.length === 1 && lista.json.items[0].correo === 'huesped@example.com')
  const det = lista.json.items[0]
  ok('admin: detalle con enlace para el huésped y nota', det.enlaceHuesped.endsWith(`/reserva/${r1.json.codigo}?t=${r1.json.token}`) && det.notaHuesped === 'Llegamos tarde.')
  ok('admin: estado inválido en el filtro → 400', (await api('GET', '/api/admin/reservas?estado=raro')).status === 400)
  ok('admin: guardar notas', (await api('PUT', `/api/admin/reservas/${det.id}/notas`, { body: { notas: 'Pidió cuna.' } })).json?.notas === 'Pidió cuna.')
  if (!PAGO) {
    const conf = await api('POST', `/api/admin/reservas/${det.id}/confirmar`)
    ok('admin: confirmar a mano (pago por otro medio)', conf.status === 200 && conf.json.estado === 'confirmada' && conf.json.pagoProveedor === 'manual')
  }
  ok('admin: confirmar una confirmada → 409', (await api('POST', `/api/admin/reservas/${det.id}/confirmar`)).status === 409)
  ok('admin: cancelar sin motivo → 400', (await api('POST', `/api/admin/reservas/${det.id}/cancelar`, { body: { motivo: '' } })).status === 400)
  const canc = await api('POST', `/api/admin/reservas/${det.id}/cancelar`, { body: { motivo: 'El huésped cambió de planes.', avisarHuesped: true } })
  ok('admin: cancelar → cancelada y libera las fechas', canc.status === 200 && canc.json.estado === 'cancelada' && (await cot({ llegada: '2027-06-08', salida: '2027-06-13', huespedes: 2 })).json.disponible === true)
  ok('admin: cancelar otra vez → 409', (await api('POST', `/api/admin/reservas/${det.id}/cancelar`, { body: { motivo: 'x' } })).status === 409)
  ok('consulta pública muestra el motivo de la cancelación', (await consultar(r1.json)).json.motivoEstado === 'El huésped cambió de planes.')

  // Bloqueos manuales
  const bl = await api('POST', `/api/admin/alojamientos/${aloj.id}/bloqueos`, { body: { desde: '2027-08-01', hasta: '2027-08-05', motivo: 'Mantenimiento' } })
  ok('bloqueo manual → 201 y ocupa el calendario', bl.status === 201 && (await cot({ llegada: '2027-08-03', salida: '2027-08-06', huespedes: 2 })).json.disponible === false)
  ok('bloqueo manual aparece en el feed .ics', (await (await api('GET', rutaIcal, { auth: false, raw: true })).text()).includes('DTSTART;VALUE=DATE:20270801'))
  ok('borrar bloqueo → 204', (await api('DELETE', `/api/admin/bloqueos/${bl.json.id}`)).status === 204)

  const dash = (await api('GET', '/api/admin/dashboard')).json
  ok('dashboard trae contadores de reservas', typeof dash.reservasPendientesPago === 'number' && typeof dash.reservasEnRevision === 'number' && typeof dash.reservasProximas === 'number')
  ok('catálogos traen los estados de reserva', (await api('GET', '/api/admin/catalogos')).json?.estadosReserva?.includes('revision'))

  // Correo (si Mailpit corre en el puerto por defecto)
  const mailpit = env.MAILPIT_URL ?? 'http://localhost:8027'
  const buzon = await fetch(`${mailpit}/api/v1/search?query=${encodeURIComponent(r1.json.codigo)}`).then((r) => (r.ok ? r.json() : null)).catch(() => null)
  if (buzon) {
    const asuntos = buzon.messages.map((m) => m.Subject)
    ok('correo al equipo por la reserva creada', asuntos.some((a) => a.includes(`Reserva ${r1.json.codigo} creada`)), asuntos.join(' | '))
    ok('correo al huésped por la cancelación', asuntos.some((a) => a.includes(`Reserva ${r1.json.codigo} cancelada`)))
    if (PAGO && SECRETO_EVENTOS) ok('correo de confirmación al huésped', asuntos.some((a) => a.includes(`Reserva confirmada ${r1.json.codigo}`)))
  } else {
    console.log('  (Mailpit no disponible: se omiten las comprobaciones de correo)')
  }

  // Integridad: con reservas no se borra el alojamiento ni el proyecto.
  ok('borrar alojamiento con reservas → 409', (await api('DELETE', `/api/admin/alojamientos/${aloj.id}`)).status === 409)
  ok('borrar proyecto con reservas → 409', (await api('DELETE', `/api/admin/proyectos/${pr.id}`)).status === 409)
  const despub = await api('PUT', `/api/admin/alojamientos/${aloj.id}`, { body: { ...datosAloj, imagenMediaId: null, publicado: false } })
  ok('despublicar alojamiento → ya no se cotiza (404)', despub.status === 200 && (await cot({ llegada: '2027-09-01', salida: '2027-09-03', huespedes: 2 })).status === 404)

  // Limpieza: las reservas solo se pueden borrar en la base.
  if (dockerOk) {
    psql(`DELETE FROM reservas WHERE alojamiento_id = ${aloj.id}`)
    ok('borrar alojamiento sin reservas → 204', (await api('DELETE', `/api/admin/alojamientos/${aloj.id}`)).status === 204)
    ok('borrar proyecto de prueba → 204', (await api('DELETE', `/api/admin/proyectos/${pr.id}`)).status === 204)
  } else {
    console.log(`  (sin docker: quedan datos de prueba en el proyecto ${pr.id})`)
  }
}

// ─── 8. Limpieza y cambio de contraseña ─────────────────────────────────────
seccion('Limpieza')
{
  ok('borrar proyecto 1 → 204', (await api('DELETE', `/api/admin/proyectos/${p1.id}`)).status === 204)
  ok('borrar proyecto 2 → 204', (await api('DELETE', `/api/admin/proyectos/${p2.id}`)).status === 204)
  ok('borrar media A libre → 204', (await api('DELETE', `/api/admin/media/${mediaA.id}`)).status === 204)
  ok('borrar media B libre → 204', (await api('DELETE', `/api/admin/media/${mediaB.id}`)).status === 204)
  const gone = await fetch(MEDIA_ROOT ? MEDIA_ROOT + mediaA.url.replace(/^\/media/, '') : BASE + mediaA.url)
  ok('el objeto desapareció del bucket (404)', gone.status === 404)
  ok('media inexistente → 404', (await api('DELETE', `/api/admin/media/${mediaA.id}`)).status === 404)
}

seccion('Cambio de contraseña y logout')
{
  const nueva = 'Otra-Clave-2026x'
  ok('password actual incorrecta → 400', (await api('POST', '/api/auth/cambiar-password', { body: { passwordActual: 'mala', passwordNueva: nueva } })).status === 400)
  ok('password débil → 400', (await api('POST', '/api/auth/cambiar-password', { body: { passwordActual: PASSWORD, passwordNueva: 'corta' } })).status === 400)
  ok('cambiar password → 204', (await api('POST', '/api/auth/cambiar-password', { body: { passwordActual: PASSWORD, passwordNueva: nueva } })).status === 204)
  ok('refresh anterior queda inválido', (await api('POST', '/api/auth/refresh', { body: { refreshToken: refresh }, auth: false })).status === 401)
  ok('la clave vieja ya no entra', (await api('POST', '/api/auth/login', { body: { correo: CORREO, password: PASSWORD }, auth: false })).status === 401)
  const l = await api('POST', '/api/auth/login', { body: { correo: CORREO, password: nueva }, auth: false })
  ok('la clave nueva entra', l.status === 200)
  token = l.json.tokens.accessToken
  ok('restaurar password original → 204', (await api('POST', '/api/auth/cambiar-password', { body: { passwordActual: nueva, passwordNueva: PASSWORD } })).status === 204)

  const l2 = await api('POST', '/api/auth/login', { body: { correo: CORREO, password: PASSWORD }, auth: false })
  token = l2.json.tokens.accessToken
  ok('logout → 204', (await api('POST', '/api/auth/logout')).status === 204)
  ok('tras logout el refresh no sirve', (await api('POST', '/api/auth/refresh', { body: { refreshToken: l2.json.tokens.refreshToken }, auth: false })).status === 401)
}

console.log(`\n${pasadas} pruebas OK, ${fallos.length} fallidas.`)
if (fallos.length) {
  console.log('Fallidas:\n - ' + fallos.join('\n - '))
  process.exit(1)
}
