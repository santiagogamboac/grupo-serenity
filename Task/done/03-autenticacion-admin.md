# 03 — Autenticación del admin

**Depende de:** 02 · **Estado:** done — 2026-09-27

## Alcance
- `POST /api/auth/login|refresh|logout`, `GET /api/auth/me`, `POST /api/auth/cambiar-password`.
- JWT access token (60 min) + refresh token opaco (64 bytes, guardado como SHA-256, rotación en cada refresh, revocación en logout/cambio de password).
- Bloqueo tras 5 intentos fallidos durante 15 min (423). Mensaje genérico ante credenciales inválidas (sin enumeración de usuarios).
- Política de autorización `AdminOnly` en todo `/api/admin/*`.
- Política de password: mínimo 10 caracteres, al menos 1 letra y 1 número.

## Aceptación
- [x] Login correcto devuelve `user` + `tokens`; token permite llamar a `/api/admin/dashboard`.
- [x] Sin token → 401; token manipulado → 401.
- [x] 5 fallos → 423 aunque luego se envíe la password correcta; desbloquea al vencer.
- [x] Refresh rota el token; reutilizar el anterior → 401.
- [x] Cambiar password invalida los refresh tokens existentes.
- [x] Tests unitarios de la lógica de bloqueo.

## Evidencia (2026-09-27)
`scripts/smoke.mjs` (login, refresh con rotación, reuso → 401, cambio de contraseña invalida refresh, logout). Bloqueo probado en vivo: 4×401, 5.º y 6.º → 423 aun con clave correcta; desbloqueo por SQL → 200. 5 tests unitarios de `Usuario` (bloqueo/vencimiento) + validación de política de contraseña.
