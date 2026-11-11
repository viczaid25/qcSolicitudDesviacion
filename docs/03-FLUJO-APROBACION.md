# Flujo de aprobación

## Estados de una solicitud (`QcDesvCodigos.EstadoSolicitud`)

| Código | Significado |
|---|---|
| `BORRADOR` | Creada pero no enviada (no se usa activamente hoy: `Crear.razor` crea y envía en un solo paso) |
| `EN_REVISION_QMS` | Primer paso, siempre |
| `EN_REVISION_APQP` | Coordinador APQP |
| `EN_REVISION_COORD_QC` | Coordinador QC |
| `EN_REVISION_MANAGER_QC` | Manager QC |
| `EN_REVISION_MANAGER_PROD_ING` | Manager Producción/Ingeniería |
| `EN_REVISION_ADVISOR_QC` | Advisor QC (último paso del flujo estándar) |
| `EN_REVISION_DGM_QC` / `EN_REVISION_DGM_PROD_ING` | Escalamiento DGM (definido en catálogo, no usado por el flujo default actual) |
| `APROBADA` | Todos los pasos aprobados |
| `RECHAZADA` | Cualquier paso rechazó |
| `CANCELADA` | El solicitante la canceló |
| `EXTENSION_SOLICITADA` | Hay una extensión de vigencia en curso |
| `EXTENSION_APROBADA` | La extensión fue aprobada (actualiza `FechaVigencia`) |
| `CERRADA` | Cierre con datos de control capturado |

El **orden real de los pasos y qué rol corresponde a cada uno vive en la tabla
`qcDesvFlujoAprobacion`** (ver [02-BASE-DE-DATOS.md](02-BASE-DE-DATOS.md)), no en C#. La lista de
arriba es la que existe hoy en el catálogo, pero cuál se usa y en qué orden depende de esa tabla.

## Roles (`QcDesvCodigos.Rol`)

`SOLICITANTE`, `QMS`, `COORD_APQP`, `COORD_QC`, `MANAGER_QC`, `MANAGER_PROD_ING`, `ADVISOR_QC`,
`DGM_QC`, `DGM_PROD_ING`, `ADMIN`.

## Quién puede ver y resolver un paso pendiente

Desde la corrección aplicada en `AprobacionService`, **cualquier usuario con el rol del paso puede
verlo en su bandeja y aprobarlo/rechazarlo** — no solo la persona específicamente elegida como
aprobador al crear la solicitud:

- `ObtenerPendientesPorUsuarioAsync`: incluye un paso si `UsuarioAprobadorId` coincide con el
  usuario **o** si el usuario tiene el rol de ese paso (`rolesUsuario.Contains(x.RolAprobadorCodigo)`).
- `UsuarioPuedeResponderAprobacionAsync`: mismo criterio para el permiso real de aprobar/rechazar.

**Caso especial QMS**: el paso QMS nunca se asigna a una persona (`UsuarioAprobadorId` siempre
`NULL` para ese rol). Lo resuelve **cualquier usuario con rol `ADMIN`** — el código mapea
`RolAprobadorCodigo == QMS` → verificar rol `ADMIN` en vez de `QMS`. La notificación de ese paso
específico va siempre al buzón fijo `EmailSettings:QmsNotificationEmail`
(`meax_qms_communication@meax.mx`), no a personas individuales.

El aprobador "seleccionado" al crear la solicitud (`AprobadoresSeleccionados` en el formulario) se
sigue guardando en `UsuarioAprobadorId` — es informativo (quién fue elegido originalmente) pero ya
no es exclusivo.

## Ciclo de vida

```
Crear.razor ──CrearYEnviarAsync──▶ EN_REVISION_QMS
                                        │ AprobarAsync (avanza al siguiente Orden pendiente)
                                        ▼
                              EN_REVISION_APQP → ... → EN_REVISION_ADVISOR_QC
                                        │
                         ┌──────────────┴───────────────┐
                    AprobarAsync                   RechazarAsync
                    (último paso)                        │
                         ▼                                ▼
                     APROBADA                        RECHAZADA
                         │                                │
                 SolicitarExtension               Editar.razor (corregir)
                 (máx 2, ≤6 meses)                        │
                         │                    ActualizarYReenviarAsync
                         ▼                       (regenera el flujo desde
                 EXTENSION_APROBADA               Orden 1 / EN_REVISION_QMS)
                         │
                 CerrarDesviacion.razor
                 (solo el solicitante original)
                         ▼
                     CERRADA
```

`CancelarAsync` puede aplicarse desde cualquier estado abierto por el propio solicitante.

## Rechazo → corrección → reenvío

`ActualizarYReenviarAsync` (`SolicitudDesviacionService`):

1. Borra **todas** las filas `QcDesvAprobacion` de la solicitud (`ExtensionId = NULL`).
2. Antes de borrarlas, agrupa por `RolAprobadorCodigo` y toma el `UsuarioAprobadorId` más reciente
   por rol — esto se pasa a `CrearAprobacionesAsync` para que el flujo regenerado **conserve los
   mismos aprobadores** en vez de quedar todos sin asignar (bug corregido: antes, cualquier
   reenvío hacía que *todas* las notificaciones cayeran en "avisar a todo el rol" en vez de a la
   persona específica, porque `UsuarioAprobadorId` quedaba en `NULL`).
3. Regenera el flujo completo desde el primer paso (`EN_REVISION_QMS`).

`Editar.razor` es también el único lugar (además de `Crear.razor`) donde se pueden **adjuntar o
quitar archivos** después de la creación inicial — útil cuando el rechazo fue justamente por falta
de evidencia.

## Extensiones (`ExtensionService`)

- Máximo 2 por solicitud (`NumeroExtension BETWEEN 1 AND 2`, reforzado por trigger
  `qcDesvTR_Extension_ValidarLimite` y `UQ(SolicitudId, NumeroExtension)`).
- La nueva fecha de vigencia no puede exceder **6 meses desde `FechaCreacion`** de la solicitud
  original (validado en código, no en BD).
- Genera su propio conjunto de `QcDesvAprobacion` con `TipoFlujoCodigo = EXTENSION` y
  `ExtensionId` distinto de `NULL` (índice único independiente del flujo normal).
- Al aprobarse el último paso: actualiza `FechaVigencia`, `NumeroExtensionActual`, resetea
  `NotificacionVencidaEnviada` y pasa el estado a `EXTENSION_APROBADA`.

## Cierre (`CierreDesviacionService`)

Solo permitido si: el usuario actual **es** `UsuarioSolicitanteId`, el estado es `APROBADA` o
`EXTENSION_APROBADA`, y no existe ya un `QcDesvControl` para esa solicitud (1-a-1). Captura lote y
case de inicio/final.

## Notificaciones por vencimiento

`DeviationExpirationNotificationService` (hosted service) corre cada 6 horas: busca solicitudes con
`FechaVigencia < hoy`, estado no terminal (`CERRADA`/`CANCELADA`/`RECHAZADA` excluidos), y
`NotificacionVencidaEnviada = false`; notifica una sola vez y marca la bandera.

## Administración de aprobadores (`/admin/aprobadores`)

Solo accesible a usuarios con rol `ADMIN` (`AdminAprobadoresService.UsuarioEsAdminAsync`). Permite:
buscar/dar de alta usuarios existentes en `meax_all_user`, asignar/quitar roles de aprobador por
usuario, activar/desactivar usuarios. **Los roles `ADMIN` y `QMS` están excluidos a propósito** de
la lista de roles asignables desde la UI — asignar `ADMIN` requiere un `INSERT` directo en
`qcDesvUsuarioRol` (da acceso administrativo completo, no solo capacidad de aprobar QMS).
