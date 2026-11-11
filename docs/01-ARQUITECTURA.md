# Arquitectura

## Stack

| Capa | Tecnología |
|---|---|
| Framework | .NET 8, Blazor Server (`AddInteractiveServerComponents`, render mode `InteractiveServer` por página) |
| Acceso a datos | Entity Framework Core (`IDbContextFactory<ApplicationDbContext>`, SQL Server) |
| Autenticación | Cookie + JWT híbrido (ver [04-AUTENTICACION.md](04-AUTENTICACION.md)) |
| UI | Bootstrap 5 (utilidades) + sistema visual propio **MEAX One** (`mo-*`, ver [DESIGN_SYSTEM.md](../DESIGN_SYSTEM.md)) |
| Correo | `System.Net.Mail.SmtpClient` vía `EmailNotificationService` |
| Hosting | IIS, como sub-aplicación de `meax.one` (PathBase dinámico) |

## Estructura del proyecto

```
qcSolicitudDesviacion/
├── Auth/                   Extensiones de autenticación (dev y hub) + contrato de claims
├── Components/
│   ├── Layout/              TopBar, NavMenu, MainLayout (renderizan en modo estático, ver nota abajo)
│   ├── Pages/
│   │   ├── Admin/            Administración de aprobadores
│   │   ├── Aprobaciones/     Bandeja de pendientes + pantalla de revisión
│   │   └── Solicitudes/      Alta, edición, detalle, cancelación, cierre, extensión, ficha compartible
│   └── App.razor            Shell HTML, <head>, referencias a JS/CSS globales
├── Pages/Account/           Login.cshtml / Logout.cshtml (Razor Pages, solo modo desarrollo)
├── Constants/               Códigos de estado/rol (QcDesvCodigos) y helper de color de badges
├── Models/                  Entidades EF Core (catálogos, solicitud, workflow, seguridad)
├── ViewModels/               Modelos de formulario (no son las entidades EF directamente)
├── Services/                 Toda la lógica de negocio (ver tabla abajo)
├── Data/ApplicationDbContext.cs
├── Database/Scripts/         Scripts SQL de apoyo (seed de usuarios demo)
└── wwwroot/                  CSS/JS estáticos, incluida la copia verbatim de meaxone-template.css
```

## Rutas (páginas Blazor)

| Ruta | Página | Notas |
|---|---|---|
| `/` | `Home.razor` | Dashboard con KPIs y solicitudes recientes |
| `/solicitudes` | `Solicitudes/Index.razor` | "Mis solicitudes" |
| `/solicitudes/crear` | `Solicitudes/Crear.razor` | Alta de solicitud + primer envío a flujo |
| `/solicitudes/{id}` | `Solicitudes/Detalle.razor` | Detalle completo, solo lectura |
| `/solicitudes/{id}/editar` | `Solicitudes/Editar.razor` | Corrección y reenvío tras rechazo |
| `/solicitudes/{id}/cancelar` | `Solicitudes/Cancelar.razor` | Cancelación por el solicitante |
| `/solicitudes/{id}/cerrar` | `Solicitudes/CerrarDesviacion.razor` | Cierre con datos de control |
| `/solicitudes/{id}/extension` | `Solicitudes/SolicitarExtension.razor` | Solicitar extensión de vigencia |
| `/solicitudes/{id}/compartir` | `Solicitudes/FichaCompartir.razor` | Ficha imprimible/exportable como PNG |
| `/desviaciones` | `Solicitudes/Desviaciones.razor` | Listado global (todas, no solo propias) |
| `/solicitudes/aprobadas` | `Solicitudes/Aprobadas.razor` | Solo aprobadas/cerradas, compartibles |
| `/aprobaciones/pendientes` | `Aprobaciones/Pendientes.razor` | Bandeja de aprobación por rol |
| `/aprobaciones/revisar/{id}` | `Aprobaciones/Revisar.razor` | Aprobar/rechazar un paso |
| `/admin/aprobadores` | `Admin/Aprobadores.razor` | Alta de usuarios MEAX + asignación de roles |
| `/Account/Login`, `/Account/Logout` | Razor Pages | Solo existen con lógica activa en modo desarrollo |

## Capa de servicios

Todos son `Scoped`, inyectados por constructor, y usan `IDbContextFactory<ApplicationDbContext>`
(nunca un `DbContext` inyectado directo) para poder abrir contextos cortos por operación.

| Servicio | Responsabilidad |
|---|---|
| `SolicitudDesviacionService` | Crear/editar/reenviar/cancelar solicitudes; consultas de detalle y resumen |
| `QcDesvWorkflowService` | Generar el conjunto de `QcDesvAprobacion` para un flujo (solicitud o extensión) a partir de `qcDesvFlujoAprobacion` |
| `AprobacionService` | Listar pendientes por usuario, aprobar, rechazar |
| `ExtensionService` | Solicitar extensión de vigencia (máx. 2, tope 6 meses desde creación) |
| `CierreDesviacionService` | Cerrar una desviación aprobada con datos de control (lote/case inicio y final) |
| `ArchivoService` | Subir/eliminar archivos adjuntos (guardados en `wwwroot/uploads/qcDesv/{solicitudId}/`) |
| `EmailNotificationService` | Construye y envía las notificaciones (paso emitido, rechazo, aprobación final, vencimiento) |
| `DeviationExpirationNotificationService` | `BackgroundService`: cada 6h revisa solicitudes vencidas y notifica una sola vez |
| `AdminAprobadoresService` | Buscar/alta de usuarios desde `meax_all_user`, gestión de roles aprobadores |
| `CatalogoService` | Catálogos de solo lectura (tipo de desviación, requisito de proceso) |
| `UsuarioActualService` | Resuelve el usuario autenticado actual a partir de los claims de la sesión |
| `UsuarioProvisioningService` | Alta/actualización del usuario local a partir de los datos del hub |

## Nota importante: render mode estático del layout

`TopBar.razor`, `NavMenu.razor` y `MainLayout.razor` se renderizan en **modo estático (SSR no
interactivo)** aunque las páginas hijas usen `@rendermode InteractiveServer`. Esto significa que
`@onclick` **no funciona** en esos tres componentes — por eso el menú hamburguesa, el dropdown de
usuario y el logout usan `onclick="..."` de JavaScript plano (`wwwroot/js/meaxone-app.js`) o un
`<a href>` normal, nunca un manejador de evento de Blazor. Si se agrega interactividad nueva en el
layout, hay que replicar ese patrón (JS plano) en vez de esperar que un `@onclick` dispare.

## Aislamiento de CSS por componente

Cada `Componente.razor` con un `Componente.razor.css` junto a él recibe un atributo de scope único
(`[b-xxxx]`) inyectado por el compilador de Blazor — las reglas de ese archivo **no** se filtran a
otros componentes. Los estilos verdaderamente globales (MEAX One, fuentes, `app.css`) están
enlazados en `<head>` desde `Components/App.razor`.
