# Base de datos

SQL Server. Base configurada en `ConnectionStrings:DefaultConnection` (`appsettings.json`).
Convención de nombres: tablas/columnas en `PascalCase` con prefijo `qcDesv`; catálogos con llave
primaria `Codigo` (string); tablas transaccionales con llave `Id` (int identity).

> ⚠️ La cadena de conexión actual guarda usuario/contraseña de SQL **en texto plano dentro de
> `appsettings.json`, versionado en el repo**. Es una deuda de seguridad pendiente — lo ideal es
> moverla a una variable de entorno de IIS, `dotnet user-secrets` (dev) o un almacén de secretos,
> y dejar el archivo solo con un placeholder.

## Catálogos (llave = `Codigo`)

Todos con `NombreEs`, `NombreEn`, `Activo`. Los valores válidos están espejados en
[`Constants/QcDesvCodigos.cs`](../qcSolicitudDesviacion/Constants/QcDesvCodigos.cs) — esa clase es
la fuente de verdad en código, pero el **orden del flujo de aprobación vive en la base**, no en C#
(ver tabla `qcDesvFlujoAprobacion` abajo).

| Tabla | Contenido |
|---|---|
| `qcDesvCatTipoDesviacion` | `CRITICA` / `ESTANDAR` |
| `qcDesvCatRequisitoProceso` | Categorías 6M: `METODO`, `MATERIA`, `MANO_OBRA`, `MAQUINA`, `MEDICION`, `OTRO` |
| `qcDesvCatEstadoSolicitud` | Todos los estados de una solicitud (ver [03-FLUJO-APROBACION.md](03-FLUJO-APROBACION.md)) |
| `qcDesvCatEstadoAprobacion` | `PENDIENTE`, `APROBADO`, `RECHAZADO`, `CANCELADO`, `OMITIDO` |
| `qcDesvCatEstadoExtension` | `PENDIENTE`, `APROBADA`, `RECHAZADA`, `CANCELADA` |
| `qcDesvCatTipoFlujo` | `SOLICITUD` / `EXTENSION` |
| `qcDesvRol` | Roles del sistema: `SOLICITANTE`, `QMS`, `COORD_APQP`, `COORD_QC`, `MANAGER_QC`, `MANAGER_PROD_ING`, `ADVISOR_QC`, `DGM_QC`, `DGM_PROD_ING`, `ADMIN` |

## Seguridad

```
qcDesvUsuario (Id, UserName [único], NombreCompleto, Email [único], Departamento,
               UnidadNegocio, Activo, FechaCreacion)
qcDesvUsuarioRol (UsuarioId, RolCodigo)  -- llave compuesta, muchos-a-muchos
```

`qcDesvUsuario` se puebla automáticamente al primer login (ver
[04-AUTENTICACION.md](04-AUTENTICACION.md)) — no hay alta manual de usuarios "de cero"; el rol
`ADMIN` en particular **debe asignarse por SQL directo** (`INSERT INTO qcDesvUsuarioRol`), la
pantalla `/admin/aprobadores` lo excluye a propósito de la lista de roles asignables desde la UI.

## Flujo de aprobación (configuración, no hardcode)

```
qcDesvFlujoAprobacion (Id, TipoFlujoCodigo, Orden, RolCodigo, EstadoSolicitudCodigo,
                       NombrePasoEs, NombrePasoEn, Activo)
  UNIQUE (TipoFlujoCodigo, Orden)
```

`QcDesvWorkflowService.CrearAprobacionesAsync` lee esta tabla ordenada por `Orden` y genera una fila
`QcDesvAprobacion` por cada paso activo. **Cambiar el orden o agregar/quitar un rol del flujo es una
operación de datos (UPDATE/INSERT en esta tabla), no un cambio de código.**

## Solicitud y su detalle

```
qcDesvSolicitud (Id, UsuarioSolicitanteId, NombreSolicitante, FechaSolicitud,
                 DepartamentoSolicitante, UnidadNegocioAfectada, CodigoLinea, Clientes,
                 TipoDesviacionCodigo, FechaVigencia, CantidadPiezas?, NumeroParteAfectado,
                 RequisitoProcesoCodigo, RequisitoProcesoOtro?, CSR1?, CSR2?, CSR3?,
                 InformarCliente, RazonInformarCliente,
                 EstadoSolicitudCodigo [default BORRADOR], NumeroExtensionActual [0-2],
                 NotificacionVencidaEnviada, CreadoPorUsuarioId, FechaCreacion,
                 ModificadoPorUsuarioId?, FechaModificacion?, RowVersion [rowversion])
```

Check constraints relevantes:
- `FechaVigencia >= FechaSolicitud`
- `CantidadPiezas IS NULL OR CantidadPiezas > 0`
- `NumeroExtensionActual BETWEEN 0 AND 2`
- Si `RequisitoProcesoCodigo = 'OTRO'` entonces `RequisitoProcesoOtro` es obligatorio.

Tablas 1-a-1 o 1-a-muchos colgando de una solicitud:

| Tabla | Cardinalidad | Notas |
|---|---|---|
| `qcDesvDescripcion` | 1-a-1 (`UQ` en `SolicitudId`) | Los 4 campos de texto de "cómo se hace / qué cambia" |
| `qcDesvAnalisisRiesgo` | 1-a-1 (`UQ` en `SolicitudId`) | Solo para `TipoDesviacionCodigo = CRITICA` — trigger `qcDesvTR_AnalisisRiesgo_SoloCritica` lo hace cumplir en BD. `RpnActual`/`RpnConCambio` son **columnas computadas y persistidas** (`Severidad × Ocurrencia × Detección`) |
| `qcDesvAccion` | 1-a-muchos | Acciones correctivas/de mitigación; puede tener archivos propios |
| `qcDesvArchivo` | 1-a-muchos | `SolicitudId` obligatorio, `AccionId` opcional. Ver nota abajo |
| `qcDesvControl` | 1-a-1 (`UQ` en `SolicitudId`) | Datos de cierre (lote/case inicio y final) |
| `qcDesvExtension` | 1-a-muchos | Máx. 2 por solicitud — trigger `qcDesvTR_Extension_ValidarLimite` + `UQ(SolicitudId, NumeroExtension)` |
| `qcDesvAprobacion` | 1-a-muchos | Un registro por paso del flujo, para la solicitud o para una extensión |
| `qcDesvHistorial` | 1-a-muchos | Bitácora de todo movimiento de estado |
| `qcDesvSolicitudCsr` | 1-a-muchos | CSRs adicionales más allá de `CSR1..3` |

### `qcDesvArchivo` — dónde aparece cada adjunto

`AccionId` es **nullable**. Los archivos subidos desde `Crear.razor` (paso "Acciones") se guardan
con `AccionId = NULL` — por diseño de la app, esto los hace aparecer en el panel superior
**"Archivos / Files"** de `Detalle.razor`/`Revisar.razor` (que lista `solicitud.Archivos` sin
filtrar por `AccionId`), no dentro de la tarjeta de una acción específica. El archivo físico vive en
`wwwroot/uploads/qcDesv/{SolicitudId}/{guid}.ext` — la ruta se guarda en `RutaRelativa`.

### `qcDesvAprobacion` — llaves únicas por tipo de flujo

```sql
UQ_qcDesvAprobacion_Normal    (SolicitudId, Orden) WHERE ExtensionId IS NULL
UQ_qcDesvAprobacion_Extension (SolicitudId, ExtensionId, Orden) WHERE ExtensionId IS NOT NULL
CK_qcDesvAprobacion_TipoExtension:
  (TipoFlujoCodigo = 'SOLICITUD' AND ExtensionId IS NULL)
  OR (TipoFlujoCodigo = 'EXTENSION' AND ExtensionId IS NOT NULL)
```

`UsuarioAprobadorId` es **nullable**: si no hay un aprobador específico asignado (o si el paso es
`QMS`), la resolución de quién puede actuar y a quién se notifica cae por **rol** — ver
[03-FLUJO-APROBACION.md](03-FLUJO-APROBACION.md).

## Vista de resumen (solo lectura, usada en listados)

```
qcDesvVW_SolicitudesResumen → QcDesvSolicitudResumen (llave: SolicitudId)
```

Trae ya resueltos los nombres ES/EN de tipo, requisito y estado — evita joins repetidos en cada
página de listado (`Desviaciones.razor`, `Index.razor`, `Aprobadas.razor`, `Home.razor`).

## Tabla externa: `meax_all_user`

No pertenece a este esquema — es la tabla de MEAX One/RR.HH. (sincronizada por un job de SQL Agent
externo, `GeneralInfoUser - Sincronizacion diaria de expedientes`, contra el sistema Tress). Se
consulta por SQL crudo (`context.Database.SqlQuery<string>($"...")`) desde
`UsuarioProvisioningService` y `UsuarioActualService` solo para resolver el **departamento** real
del usuario por `pc_login_id`/`username`. No se escribe nunca desde esta app.

## Incidente conocido: identity reseed en `qcDesvSolicitud`

En septiembre 2026 el `Id` de `qcDesvSolicitud` saltó dos veces de golpe **+999** (p. ej. de 7 a
1007, luego de 15 a 1014). La causa raíz fue un *linked server* llamado `SRV_ORIGEN` apuntando al
servidor de base de datos viejo ya decomisionado (`10.228.25.13`) — se eliminó
(`sp_dropserver 'SRV_ORIGEN', 'droplogins'`). Si vuelve a pasar:

1. Confirmar el salto: `SELECT IDENT_CURRENT('dbo.qcDesvSolicitud')` vs. el máximo `Id` real.
2. Buscar la causa antes de corregir: `sys.servers` (linked servers), `msdb.dbo.sysjobs` /
   `sysjobsteps` (jobs que referencien la tabla o un servidor externo), Programador de tareas de
   Windows en el servidor de BD.
3. Renumerar (requiere `SET IDENTITY_INSERT ON`, **no** se puede hacer `UPDATE` directo sobre una
   columna identity): insertar copias de las filas afectadas con el `Id` correcto, repuntar las 9
   tablas hijas (`qcDesvAccion`, `qcDesvAnalisisRiesgo`, `qcDesvAprobacion`, `qcDesvArchivo`,
   `qcDesvControl`, `qcDesvDescripcion`, `qcDesvExtension`, `qcDesvHistorial`,
   `qcDesvSolicitudCsr`), borrar las filas viejas, y `DBCC CHECKIDENT (..., RESEED, <max real>)`.
   Verificar 0 filas huérfanas en las 9 tablas antes de dar por cerrado.
