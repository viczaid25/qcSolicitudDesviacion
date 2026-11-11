# Sistema de diseño — MEAX One

> Este documento reemplaza la guía anterior ("Midnight Moderno"), que quedó obsoleta cuando la
> interfaz se migró al sistema de diseño oficial **MEAX One** (compartido con meaxHub y otras apps
> satélite). Si buscas la paleta/clases viejas (`.nueva-hero`, `.table-btn`, `.panel-badge`,
> `.form-control` como clase visual propia, etc.) — ya no se usan; lo que sigue es lo vigente.

## Fuente de verdad

- **`wwwroot/css/meaxone-template.css`** — copia **verbatim** del design system de MEAX One. No se
  edita a mano salvo para traer una versión más nueva del original; cualquier ajuste específico de
  esta app va en un archivo aparte.
- **`wwwroot/css/meaxone-app.css`** — overrides puntuales *de esta app* (hoy: contraste de
  `.mo-btn-secondary-outline` sobre el fondo oscuro de `#mo-banner`). Se mantiene deliberadamente
  separado del archivo verbatim de arriba.
- **`wwwroot/js/meaxone-app.js`** — toggle de sidebar y del dropdown de usuario, en JS plano (ver
  nota de render mode en [docs/01-ARQUITECTURA.md](docs/01-ARQUITECTURA.md) — `TopBar`/`NavMenu`
  no soportan `@onclick` de Blazor).
- **`wwwroot/js/meaxone-table.js`** — filtro/paginación de tablas por `data-mo-table` (copiado, no
  está enganchado a ningún marcado todavía en esta app).
- **`wwwroot/fonts/montserrat/`** — Montserrat autohospedada (400/500/600/700/800), sin depender de
  Google Fonts por si el servidor de producción no tiene salida a internet.

Los cuatro se enlazan en `Components/App.razor`, en ese orden, después de `app.css`.

## Reglas al tocar cualquier estilo

1. **Nunca** renombrar o inventar variables/clases `mo-*` — son el contrato compartido con el resto
   de MEAX One.
2. **No existe el color "purple"/morado** en la paleta actual — si algo lo tenía de una migración
   vieja, se reemplaza por azul (`--mo-blue`/`--mo-blue-deep`) al tocarlo.
3. No introducir colores fuera de las variables ya definidas en `meaxone-template.css` — un acento
   de color nuevo se confirma con el equipo antes de inventarlo.
4. Cambios específicos de esta app van en `meaxone-app.css`, no parcheando el archivo verbatim.

## Variables clave (`:root` en `meaxone-template.css`)

| Variable | Valor | Uso |
|---|---|---|
| `--mo-navy` | `#0e0d22` | Base del degradado de navbar/sidebar/banner |
| `--mo-blue` | `#2563eb` | Azul de marca / acento principal |
| `--mo-blue-deep` | `#1e3a8a` | Azul oscuro (texto sobre fondos claros, hover) |
| `--color-primary-gradient` | `linear-gradient(135deg, #060B29 0%, #1c3375 100%)` | Fondo de `#mo-banner` en el hero de cada página |
| `--mo-success` / `-dark` | `#10b981` / `#059669` | Estados positivos |
| `--mo-warning` / `-dark` | `#f59e0b` / `#d97706` | Pendiente / por vencer |
| `--mo-danger` / `-dark` | `#ef4444` / `#dc2626` | Rechazado / vencido / eliminar |
| `--dept-*` | Varios | Acentos por departamento (no usados directamente en esta app hoy) |

## Estructura de layout

```
#mo-navbar          top bar fijo, degradado navy
#mo-sidebar          sidebar colapsable, degradado navy
#mo-content-area     wrapper de sidebar + main
#mo-main             contenido, fondo claro
#mo-banner           hero de cada página (degradado, título/subtítulo/acciones)
```

Ver `Components/Layout/TopBar.razor`, `NavMenu.razor`, `MainLayout.razor` para el marcado real —
cada página arma su propio `#mo-banner` con `#mo-banner-tag` / `#mo-banner-title` / `#mo-banner-sub`
más `.mo-banner-orb` (los dos círculos decorativos) y `.hero-actions`.

## Clases de componentes (`mo-*`) más usadas en esta app

| Familia | Clases | Ejemplo de uso |
|---|---|---|
| Botones | `.mo-btn`, `.mo-btn-primary`, `.mo-btn-secondary`, `.mo-btn-secondary-outline`, `.mo-btn-danger`, `.mo-btn-danger-outline`, sufijos `-sm`/`-block` | Ver `Crear.razor`, `Detalle.razor` |
| Badges | `.mo-badge`, `.mo-badge-primary/-secondary/-success/-warning/-danger`, `.mo-badge-dot` | Estado de solicitud/aprobación — ver `Constants/QcDesvEstadoBadge.cs` para el mapeo estado→color centralizado |
| Alertas | `.mo-alert`, `.mo-alert-primary/-success/-warning/-danger` | Mensajes de éxito/error en formularios |
| Formularios | `.mo-form-group`, `.mo-form-label`, `.mo-form-control`, `.mo-form-select`, `.mo-form-check` | Todos los `<InputText>`/`<InputSelect>`/inputs planos de las páginas de Solicitudes |
| Tablas | `.mo-table`, `.mo-table-wrap` | Dual-classed junto con clases propias de cada página (`.procesos-table`, `.mini-table`) para no perder su CSS específico |
| Paneles/tarjetas | `.mo-panel` | — |

## Convención "dual-class" en tablas reales

Las páginas con una `<table>` real (`Index.razor`, `Pendientes.razor`, `Detalle.razor`,
`Revisar.razor`) usan **dos clases a la vez**, p. ej. `class="mo-table procesos-table"`: `mo-table`
da la tipografía/paleta de MEAX One, y la clase propia de la página conserva su ancho mínimo,
paddings específicos, etc. Esto funciona porque el CSS aislado por componente (`*.razor.css`) tiene
mayor especificidad y carga después que el CSS global — la regla de la página siempre gana para lo
que ella misma define, y hereda del `mo-table` global lo que no redefine.

## Páginas sin CSS específico propio (antes de la migración)

Algunas páginas (`Editar.razor`, `Cancelar.razor`, `CerrarDesviacion.razor`,
`SolicitarExtension.razor`) no tenían un archivo `.razor.css` completo antes de la migración a MEAX
One — se les creó uno con el layout compartido (`panel-card`, `nueva-grid`, etc., más lo específico
de cada una) al mismo tiempo que se migró su marcado a clases `mo-*`.

## Parte del sistema deliberadamente sin migrar

`FichaCompartir.razor` (la ficha exportable como imagen/impresión) mantiene su propio diseño
(`.share-sheet`, `.risk-table`, `.approver-card`, etc.) **a propósito** — es un documento de marca
fija pensado para imprimirse o compartirse como PNG, no parte del "chrome" de la aplicación. Solo su
barra de herramientas (botones) usa `mo-btn`.
