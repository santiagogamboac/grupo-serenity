#!/usr/bin/env node
// Inserta destinos (proyectos) de ejemplo, cada uno con su galería y un alojamiento, usando el API del panel.
// No toca la portada (hero): su imagen principal sigue siendo la sala.
//
//   node scripts/destinos-ejemplo.mjs                              # contra http://localhost:8081 (API directa)
//   BASE=http://localhost:8090 node scripts/destinos-ejemplo.mjs   # contra nginx (web)
//   node scripts/destinos-ejemplo.mjs --limpiar                    # borra los destinos de ejemplo
//
// Credenciales: ADMIN_CORREO / ADMIN_PASSWORD del entorno o del archivo .env de la raíz.
// Idempotente: un destino que ya existe (mismo nombre) se omite; las imágenes se reutilizan por nombre de archivo.
// Pensado para desarrollo y demos: no lo corras en producción salvo que quieras esos destinos publicados.

import { readFileSync, existsSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const raiz = join(dirname(fileURLToPath(import.meta.url)), '..')
const carpetaImagenes = join(raiz, 'Backend', 'seed', 'images')
const env = { ...process.env }
if (existsSync(join(raiz, '.env'))) {
  for (const linea of readFileSync(join(raiz, '.env'), 'utf8').split(/\r?\n/)) {
    const m = linea.match(/^([A-Z0-9_]+)=(.*)$/)
    if (m && env[m[1]] === undefined) env[m[1]] = m[2]
  }
}

const BASE = (env.BASE ?? env.SMOKE_BASE ?? 'http://localhost:8081').replace(/\/$/, '')
const LIMPIAR = process.argv.includes('--limpiar')
if (!env.ADMIN_CORREO || !env.ADMIN_PASSWORD) throw new Error('Falta ADMIN_CORREO / ADMIN_PASSWORD (env o .env).')

const DESTINOS = [
  {
    nombre: 'Cartagena',
    etiqueta: 'Ciudad amurallada',
    icono: 'sailboat',
    ubicacionEtiqueta: 'Cartagena · Bolívar',
    titulo: 'Historia junto al mar.',
    descripcion: 'Calles de colores, atardeceres en la muralla y noches cálidas. Una escapada para caminar sin prisa y mirar el Caribe.',
    metaLugar: 'Centro histórico',
    metaEstancia: 'Escapadas desde 3 noches',
    imagenes: [
      { archivo: 'santa-marta.jpeg', textoAlt: 'Casa con piscina en la costa caribe' },
      { archivo: 'hero-piscina.jpeg', textoAlt: 'Piscina frente al mar' },
    ],
    alojamiento: {
      nombre: 'Apartamento Muralla',
      descripcion: 'Dos habitaciones, balcón con vista a la ciudad y piscina en la terraza.',
      imagen: 'hero-sala.jpeg',
      capacidad: 4, huespedesIncluidos: 2, precioNoche: 420000, recargoHuespedExtra: 60000, tarifaAseo: 90000, estanciaMinima: 3,
    },
  },
  {
    nombre: 'Guatapé',
    etiqueta: 'Lago',
    icono: 'compass',
    ubicacionEtiqueta: 'Guatapé · Antioquia',
    titulo: 'El agua quieta.',
    descripcion: 'Embalse, piedra y pueblo de zócalos. Mañanas en kayak y tardes en la orilla, lejos del ruido de la ciudad.',
    metaLugar: 'Orilla del embalse',
    metaEstancia: 'Escapadas desde 2 noches',
    imagenes: [
      { archivo: 'estadia-1.jpeg', textoAlt: 'Detalle de alojamiento Serenity' },
      { archivo: 'hero-sala.jpeg', textoAlt: 'Sala luminosa con vista al lago' },
    ],
    alojamiento: {
      nombre: 'Cabaña El Peñol',
      descripcion: 'Cabaña de madera con muelle propio, chimenea y zona de fogata.',
      imagen: 'estadia-1.jpeg',
      capacidad: 6, huespedesIncluidos: 4, precioNoche: 380000, recargoHuespedExtra: 50000, tarifaAseo: 80000, estanciaMinima: 2,
    },
  },
  {
    nombre: 'Salento',
    etiqueta: 'Eje cafetero',
    icono: 'coffee',
    ubicacionEtiqueta: 'Salento · Quindío',
    titulo: 'Entre palmas y café.',
    descripcion: 'Valle de Cocora, fincas cafeteras y neblina de montaña. Un lugar para respirar hondo y probar el mejor café.',
    metaLugar: 'Valle de Cocora',
    metaEstancia: 'Escapadas desde 2 noches',
    imagenes: [
      { archivo: 'hero-sala.jpeg', textoAlt: 'Interior cálido de una finca' },
      { archivo: 'estadia-1.jpeg', textoAlt: 'Detalle de alojamiento Serenity' },
    ],
    alojamiento: {
      nombre: 'Finca La Neblina',
      descripcion: 'Casa de finca con corredor, hamacas y recorrido por el cafetal.',
      imagen: 'hero-sala.jpeg',
      capacidad: 8, huespedesIncluidos: 4, precioNoche: 350000, recargoHuespedExtra: 45000, tarifaAseo: 70000, estanciaMinima: 2,
    },
  },
  {
    nombre: 'Villa de Leyva',
    etiqueta: 'Pueblo colonial',
    icono: 'sun',
    ubicacionEtiqueta: 'Villa de Leyva · Boyacá',
    titulo: 'El cielo más abierto.',
    descripcion: 'Plaza empedrada, desierto de colores y noches estrelladas. Fines de semana tranquilos con sabor a pueblo.',
    metaLugar: 'Cerca de la plaza mayor',
    metaEstancia: 'Fines de semana',
    imagenes: [
      { archivo: 'estadia-1.jpeg', textoAlt: 'Patio de una casa colonial' },
      { archivo: 'santa-marta.jpeg', textoAlt: 'Casa con piscina para descansar' },
    ],
    alojamiento: {
      nombre: 'Casa del Patio',
      descripcion: 'Casa colonial con patio central, tres habitaciones y jacuzzi.',
      imagen: 'santa-marta.jpeg',
      capacidad: 6, huespedesIncluidos: 2, precioNoche: 460000, recargoHuespedExtra: 55000, tarifaAseo: 85000, estanciaMinima: 2,
    },
  },
]

let token = null
async function api(method, ruta, { body, form } = {}) {
  const headers = {}
  if (token) headers.Authorization = `Bearer ${token}`
  let payload
  if (form) payload = form
  else if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    payload = JSON.stringify(body)
  }
  const res = await fetch(BASE + ruta, { method, headers, body: payload })
  const texto = await res.text()
  let json = null
  try {
    json = texto ? JSON.parse(texto) : null
  } catch {
    /* no JSON */
  }
  return { status: res.status, json, texto }
}

async function exigir(promesa, esperado, que) {
  const r = await promesa
  if (!esperado.includes(r.status)) throw new Error(`${que}: HTTP ${r.status} ${r.texto}`)
  return r.json
}

// ─── Medios: se reutilizan por nombre de archivo (el seed los sube con ese nombre) ───
const mediaPorArchivo = new Map()
async function cargarBiblioteca() {
  for (let page = 1; ; page++) {
    const r = await exigir(api('GET', `/api/admin/media?page=${page}&pageSize=100`), [200], 'listar medios')
    for (const m of r.items) if (!mediaPorArchivo.has(m.nombreOriginal)) mediaPorArchivo.set(m.nombreOriginal, m.id)
    if (page * r.pageSize >= r.totalCount || r.items.length === 0) break
  }
}
async function mediaId(archivo) {
  if (mediaPorArchivo.has(archivo)) return mediaPorArchivo.get(archivo)
  const ruta = join(carpetaImagenes, archivo)
  if (!existsSync(ruta)) throw new Error(`No existe la imagen ${ruta}`)
  const tipo = archivo.endsWith('.png') ? 'image/png' : archivo.endsWith('.webp') ? 'image/webp' : 'image/jpeg'
  const form = new FormData()
  form.append('archivo', new Blob([readFileSync(ruta)], { type: tipo }), archivo)
  const m = await exigir(api('POST', '/api/admin/media', { form }), [200, 201], `subir ${archivo}`)
  mediaPorArchivo.set(archivo, m.id)
  console.log(`  ↑ subida ${archivo} (media ${m.id})`)
  return m.id
}

// ─── Acciones ───────────────────────────────────────────────────────────────
async function insertarDestinos() {
  const existentes = new Set((await exigir(api('GET', '/api/admin/proyectos'), [200], 'listar proyectos')).map((p) => p.nombre))
  for (const d of DESTINOS) {
    if (existentes.has(d.nombre)) {
      console.log(`  · ${d.nombre}: ya existe, se omite`)
      continue
    }
    const { imagenes, alojamiento, ...datos } = d
    const p = await exigir(
      api('POST', '/api/admin/proyectos', { body: { ...datos, urlReserva: null, ctaTexto: 'Ver alojamientos', publicado: true } }),
      [201],
      `crear ${d.nombre}`,
    )
    for (const img of imagenes) {
      await exigir(api('POST', `/api/admin/proyectos/${p.id}/imagenes`, { body: { mediaId: await mediaId(img.archivo), textoAlt: img.textoAlt } }), [200, 201], `imagen de ${d.nombre}`)
    }
    const { imagen, ...aloj } = alojamiento
    await exigir(
      api('POST', `/api/admin/proyectos/${p.id}/alojamientos`, { body: { ...aloj, imagenMediaId: await mediaId(imagen), anuncioId: null, publicado: true } }),
      [200, 201],
      `alojamiento de ${d.nombre}`,
    )
    console.log(`  ✓ ${d.nombre} (proyecto ${p.id}, slug ${p.slug}) con ${imagenes.length} imágenes y "${alojamiento.nombre}"`)
  }
}

async function limpiar() {
  const nombres = new Set(DESTINOS.map((d) => d.nombre))
  for (const p of await exigir(api('GET', '/api/admin/proyectos'), [200], 'listar proyectos')) {
    if (!nombres.has(p.nombre)) continue
    const r = await api('DELETE', `/api/admin/proyectos/${p.id}`)
    if (r.status === 204 || r.status === 200) console.log(`  ✓ eliminado ${p.nombre}`)
    else console.log(`  ✗ ${p.nombre}: HTTP ${r.status} ${r.json?.detail ?? r.texto}`)
  }
}

// ─── Main ───────────────────────────────────────────────────────────────────
console.log(`API: ${BASE}`)
const login = await exigir(api('POST', '/api/auth/login', { body: { correo: env.ADMIN_CORREO, password: env.ADMIN_PASSWORD } }), [200], 'login')
token = login.tokens.accessToken

if (LIMPIAR) {
  console.log('\nEliminando destinos de ejemplo')
  await limpiar()
} else {
  await cargarBiblioteca()
  console.log('\nDestinos de ejemplo')
  await insertarDestinos()
}
console.log('\nListo.')
