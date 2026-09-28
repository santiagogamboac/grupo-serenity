#!/usr/bin/env node
// Smoke test E2E de la API de Serenity. Recorre auth, contenido, proyectos, reseñas,
// valores, medios y solicitudes, y deja la base como la encontró.
//
//   node scripts/smoke.mjs                         # contra http://localhost:8081 (API directa)
//   SMOKE_BASE=http://localhost:8090 node scripts/smoke.mjs   # contra nginx (web) → /api y /media
//
// Credenciales: ADMIN_CORREO / ADMIN_PASSWORD del entorno o del archivo .env de la raíz.
// Si la API se corre sin nginx, define MEDIA_ROOT=http://localhost:9012/serenity-media para bajar las imágenes.

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
const CORREO = env.ADMIN_CORREO
const PASSWORD = env.ADMIN_PASSWORD
if (!CORREO || !PASSWORD) throw new Error('Falta ADMIN_CORREO / ADMIN_PASSWORD (env o .env).')

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
