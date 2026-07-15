# Guía de diseño — "Midnight Moderno"

Guía de estilos visuales extraída de `qcSolicitudDesviacion` para replicar la misma identidad
visual en otros sistemas. Es CSS puro basado en variables (custom properties); no depende de
ningún framework de JS. Convive bien con Bootstrap 5 (algunas páginas usan sus utilidades), pero
no lo necesita: todos los bloques de este documento son autocontenidos.

## 1. Filosofía

- **Paleta "Midnight"**: superficies casi blancas (`#f8fafc`) para el contenido, contrastadas con
  bloques azul-marino muy oscuro con degradado (sidebar, top bar, "hero" de cada página).
- **Todo es redondeado**: radios grandes (16–28px) en tarjetas y héroes, pill-shape (`999px`) en
  botones, badges y chips.
- **Sombras suaves y difusas** en vez de bordes duros para dar profundidad a las tarjetas sobre el
  fondo gris claro.
- **Tipografía del sistema** (`system-ui`), pesos muy marcados (700–950) para títulos y datos
  clave, y grises azulados (`#64748b`) para texto secundario.

## 2. Design tokens (variables CSS)

Copiar tal cual en el `:root` del proyecto destino:

```css
:root {
    /* Color */
    --color-primary: #060B29;
    --color-primary-hover: #1e3a8a;
    --color-accent-vibrant: #3b82f6;
    --color-primary-gradient: linear-gradient(135deg, #060B29 0%, #1c3375 100%);
    --color-secondary: #0f172a;

    --text: #1e293b;
    --text-muted: #64748b;
    --bg: #f8fafc;
    --surface: #ffffff;
    --border: #e2e8f0;
    --input-bg: #f1f5f9;
    --placeholder: #94a3b8;

    /* Layout */
    --sidebar-w: 260px;
    --navbar-h: 64px;

    /* Radios */
    --radius: 16px;
    --radius-sm: 10px;

    /* Sombras */
    --shadow-sm: 0 1px 3px 0 rgb(0 0 0 / 0.1), 0 1px 2px -1px rgb(0 0 0 / 0.1);
    --shadow-md: 0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1);
    --shadow-lg: 0 20px 25px -5px rgb(0 0 0 / 0.1), 0 8px 10px -6px rgb(0 0 0 / 0.1);
    --shadow-primary: 0 10px 15px -3px rgba(6, 11, 41, 0.3);
    --focus: 0 0 0 4px rgba(6, 11, 41, 0.15);
}

html, body {
    height: 100%;
    margin: 0;
    font-family: system-ui, -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
    background: var(--bg);
    color: var(--text);
    -webkit-font-smoothing: antialiased;
}
```

### Paleta de referencia rápida

| Uso | Color |
|---|---|
| Fondo de app | `#f8fafc` |
| Superficie / tarjeta | `#ffffff` (u `rgba(255,255,255,0.94)` sobre fondo con blur) |
| Texto principal | `#0f172a` / `#1e293b` |
| Texto secundario | `#64748b` |
| Borde sutil | `#e2e8f0` |
| Azul marino (marca) | `#060B29` → degradado a `#1c3375` |
| Azul acento / links / primary btn | `#3b82f6` → `#1d4ed8` (degradado en botones primarios) |
| Éxito | fondo `#f0fdf4` / texto `#166534` |
| Error / peligro | fondo `#fef2f2` / texto `#991b1b` |
| Info | fondo `#eff6ff` / texto `#1d4ed8` / `#1e3a8a` |

## 3. Estructura de layout (Top bar + Sidebar + Contenido)

```
┌─────────────────────────────────────────────────────────┐
│ TOP BAR (degradado azul marino, sticky, h≈4.5rem)        │
├───────────────┬───────────────────────────────────────────┤
│               │                                           │
│  SIDEBAR      │  CONTENIDO (fondo #f8fafc)                │
│  (260–280px)  │   - "hero" de página (bloque degradado)   │
│  degradado    │   - panel-card(s)                         │
│  azul marino  │                                           │
│               │                                           │
└───────────────┴───────────────────────────────────────────┘
```

```css
.page {
    display: flex;
    flex-direction: column;
    min-height: 100vh;
    background: #f8fafc;
}

.top-bar {
    background: var(--color-primary-gradient);
    color: #fff;
    border-bottom: 1px solid rgba(255,255,255,0.08);
    position: sticky;
    top: 0;
    z-index: 30;
}

.body-row {
    display: flex;
    flex: 1;
}

.sidebar {
    width: 280px;
    min-width: 280px;
    height: calc(100vh - 4.5rem);
    position: sticky;
    top: 4.5rem;
    background: var(--color-primary-gradient);
    color: #fff;
}

main {
    flex: 1;
    background: #f8fafc;
}

.content {
    padding: 1.4rem 2rem 2rem;
}
```

### Ítems de navegación (sidebar)

```css
.nav-section-title {
    margin: 0.9rem 0 0.45rem;
    padding: 0 0.25rem;
    color: rgba(255,255,255,0.48);
    font-size: 0.72rem;
    font-weight: 900;
    text-transform: uppercase;
    letter-spacing: 0.08em;
}

.qc-nav-link {
    width: 100%;
    min-height: 58px;
    display: flex;
    align-items: center;
    gap: 0.85rem;
    padding: 0.78rem 1rem;
    border-radius: 16px;
    color: rgba(255,255,255,0.86);
    text-decoration: none;
    background: transparent;
    border: 1px solid transparent;
    transition: all 0.18s ease;
}

.qc-nav-link:hover {
    background: rgba(255,255,255,0.10);
    color: #fff;
    transform: translateX(2px);
}

.qc-nav-link.active {
    background: rgba(255,255,255,0.18);
    color: #fff;
    border-color: rgba(255,255,255,0.08);
}

.qc-nav-link.active::before {
    content: "";
    position: absolute;
    left: 0; top: 50%;
    width: 4px; height: 26px;
    transform: translateY(-50%);
    border-radius: 0 6px 6px 0;
    background: var(--color-accent-vibrant);
    box-shadow: 0 0 12px rgba(59,130,246,0.65);
}
```

### Top bar — usuario activo / logout

```css
.top-bar-inner {
    min-height: 4.5rem;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
    padding: 0.5rem 1.5rem;
}

.top-bar-user-label {
    color: rgba(255,255,255,0.52);
    font-size: 0.66rem;
    font-weight: 900;
    text-transform: uppercase;
    letter-spacing: 0.08em;
}

.top-bar-user-name {
    color: #fff;
    font-size: 0.92rem;
    font-weight: 900;
}

.top-bar-logout-btn {
    min-height: 40px;
    padding: 0 1.1rem;
    border: 1px solid rgba(255,255,255,0.18);
    border-radius: 12px;
    background: rgba(255,255,255,0.08);
    color: #fff;
    font-weight: 800;
}

.top-bar-logout-btn:hover {
    background: rgba(255,255,255,0.16);
}
```

## 4. "Hero" de página

Bloque de encabezado con degradado azul marino que abre casi todas las páginas: kicker (etiqueta),
título grande, texto de apoyo y acciones a la derecha.

```html
<section class="nueva-hero">
    <div>
        <span class="nueva-kicker">Workflow / Approval Flow</span>
        <h1>Aprobaciones pendientes / Pending Approvals</h1>
        <p class="hero-text">Solicitudes que requieren revisión de acuerdo con tus roles asignados.</p>
    </div>
    <div class="hero-actions">
        <a class="hero-btn" href="#">Acción secundaria</a>
    </div>
</section>
```

```css
.nueva-hero {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: 1rem;
    padding: 1.6rem 1.75rem;
    border-radius: 28px;
    background: var(--color-primary-gradient);
    color: #fff;
    box-shadow: 0 24px 45px rgba(15,23,42,0.16);
}

.nueva-kicker {
    display: inline-flex;
    padding: 0.38rem 0.78rem;
    border-radius: 999px;
    background: rgba(255,255,255,0.10);
    color: rgba(255,255,255,0.86);
    font-size: 0.82rem;
    font-weight: 700;
    margin-bottom: 0.9rem;
}

.nueva-hero h1 {
    margin: 0 0 0.45rem;
    font-size: clamp(1.9rem, 3vw, 2.7rem);
    font-weight: 800;
}

.hero-text {
    margin: 0;
    color: rgba(255,255,255,0.78);
}

.hero-actions {
    display: flex;
    gap: 0.75rem;
}

.hero-btn {
    min-height: 46px;
    padding: 0.78rem 1.15rem;
    border-radius: 16px;
    background: rgba(255,255,255,0.10);
    color: #fff;
    text-decoration: none;
    font-weight: 700;
    border: 1px solid rgba(255,255,255,0.08);
}

.hero-btn:hover {
    background: rgba(255,255,255,0.16);
}

/* Variante de acción destacada dentro del hero (p. ej. "Crear") */
.hero-btn.primary {
    background: linear-gradient(135deg, #3b82f6 0%, #1d4ed8 100%);
    border-color: transparent;
    box-shadow: 0 12px 24px rgba(59,130,246,0.25);
}
```

> Variante alterna usada en pantallas de administración: degradado `linear-gradient(135deg, #0f172a, #1e1b4b)`
> con `hero-btn` en blanco sólido (`background:#fff; color:#1e1b4b`). Úsala cuando quieras diferenciar
> visualmente una sección "admin" del resto.

## 5. Tarjetas de contenido (`panel-card`)

Contenedor blanco translúcido, elevado sobre el fondo gris, que envuelve tablas, formularios y listas.

```css
.panel-card {
    padding: 1.25rem;
    border-radius: 24px;
    background: rgba(255,255,255,0.94);
    border: 1px solid rgba(226,232,240,0.95);
    box-shadow: 0 14px 30px rgba(15,23,42,0.06);
}

.panel-head {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: 1rem;
    margin-bottom: 1rem;
}

.panel-head h2 {
    margin: 0;
    font-size: 1.08rem;
    color: #0f172a;
    font-weight: 800;
}

.panel-subtitle {
    margin: 0.3rem 0 0;
    color: #64748b;
    font-size: 0.9rem;
}

.panel-badge {
    padding: 0.38rem 0.75rem;
    border-radius: 999px;
    background: #eff6ff;
    color: #1d4ed8;
    font-size: 0.82rem;
    font-weight: 700;
    white-space: nowrap;
}
```

### Tarjetas de estadística (KPI)

```css
.stat-card {
    padding: 1.15rem;
    border-radius: 24px;
    background: rgba(255,255,255,0.94);
    border: 1px solid rgba(226,232,240,0.95);
    box-shadow: 0 14px 30px rgba(15,23,42,0.06);
    transition: all 0.18s ease;
}

.stat-card:hover {
    transform: translateY(-2px);
    box-shadow: 0 18px 34px rgba(15,23,42,0.09);
}

.stat-card span {           /* etiqueta */
    color: #64748b;
    font-size: 0.78rem;
    font-weight: 800;
    text-transform: uppercase;
    letter-spacing: 0.04em;
}

.stat-card strong {         /* valor grande */
    display: block;
    margin-top: 0.45rem;
    color: #0f172a;
    font-size: 2.1rem;
    font-weight: 900;
}
```

## 6. Botones

| Clase | Uso |
|---|---|
| `.hero-btn` | Botón sobre fondo oscuro (dentro del hero) |
| `.hero-btn.primary` | Acción principal dentro del hero (degradado azul) |
| `.table-btn` | Botón secundario neutro (dentro de tarjetas blancas) |
| `.table-btn-primary` | Botón primario dentro de tarjetas blancas |
| `.table-btn.danger` | Acción destructiva (rechazar, eliminar) |

```css
.table-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    min-height: 40px;
    padding: 0.6rem 0.9rem;
    border-radius: 999px;
    border: 1px solid #cbd5e1;
    background: #ffffff;
    color: #334155;
    font-size: 0.84rem;
    font-weight: 900;
    text-decoration: none;
    cursor: pointer;
    box-shadow: 0 8px 18px rgba(15, 23, 42, 0.06);
    transition: transform 0.15s ease, box-shadow 0.15s ease;
}

.table-btn:hover {
    transform: translateY(-1px);
    background: #f8fafc;
    border-color: #94a3b8;
    box-shadow: 0 14px 28px rgba(15, 23, 42, 0.11);
}

.table-btn:disabled {
    opacity: 0.55;
    cursor: not-allowed;
    transform: none;
    box-shadow: none;
}

.table-btn-primary {
    border-color: #4f46e5;
    background: linear-gradient(135deg, #4f46e5, #2563eb);
    color: #ffffff !important;
    box-shadow: 0 14px 28px rgba(37, 99, 235, 0.24);
}

.table-btn-primary:hover {
    background: linear-gradient(135deg, #4338ca, #1d4ed8);
}

.table-btn.danger {
    border-color: #fecaca;
    background: #fef2f2;
    color: #991b1b;
}

.table-btn.danger:hover {
    background: #fee2e2;
    border-color: #fca5a5;
}
```

## 7. Badges, pills y chips de estado

```css
.status-pill,
.type-pill {
    display: inline-flex;
    align-items: center;
    min-height: 34px;
    padding: 0.35rem 0.75rem;
    border-radius: 999px;
    font-size: 0.78rem;
    font-weight: 800;
}

.status-pill { background: #eff6ff; color: #1d4ed8; }   /* estado informativo/azul por defecto */
.type-pill   { background: #f8fafc; color: #334155; }   /* etiqueta neutra */

/* Variantes semánticas típicas — agregar según el estado real */
.status-pill.ok      { background: #dcfce7; color: #166534; } /* aprobado / activo */
.status-pill.off      { background: #f1f5f9; color: #64748b; } /* inactivo / cancelado */
.status-pill.warning { background: #fff7ed; color: #ea580c; } /* pendiente / por vencer */
.status-pill.danger  { background: #fef2f2; color: #991b1b; } /* rechazado / vencido */

.role-chip {
    display: inline-flex;
    padding: 0.3rem 0.58rem;
    border-radius: 999px;
    background: #e0f2fe;
    color: #075985;
    font-size: 0.72rem;
    font-weight: 950;
}
```

## 8. Tablas

```css
.table-wrap { width: 100%; overflow-x: auto; }

.procesos-table {
    width: 100%;
    min-width: 920px;
    border-collapse: separate;
    border-spacing: 0;
}

.procesos-table thead th {
    padding: 0.95rem 1rem;
    font-size: 0.82rem;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    color: #64748b;
    background: #f8fafc;
    border-bottom: 1px solid #e2e8f0;
}

.procesos-table tbody td {
    padding: 1rem;
    border-bottom: 1px solid #eef2f7;
    background: #fff;
    color: #1e293b;
}

.cell-title { font-weight: 800; color: #0f172a; }
.cell-subtitle { margin-top: 0.18rem; color: #64748b; font-size: 0.85rem; }
```

## 9. Formularios

```css
.form-label {
    font-weight: 700;
    color: #334155;
    margin-bottom: 0.35rem;
}

.form-control,
.form-select {
    min-height: 48px;
    padding: 0.6rem 0.8rem;
    border-radius: 14px;
    border: 1px solid #dbe3ee;
    background: #fff;
    color: var(--text);
    box-shadow: none;
    transition: all 0.2s;
}

.form-control:focus,
.form-select:focus {
    border-color: #60a5fa;
    box-shadow: 0 0 0 0.18rem rgba(59,130,246,0.14);
}

.field-help {
    margin-top: 0.35rem;
    color: #64748b;
    font-size: 0.84rem;
    line-height: 1.35;
}

.form-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 1rem;
}

.form-grid.three { grid-template-columns: repeat(3, minmax(0, 1fr)); }
```

## 10. Estados vacíos y de carga

```css
.empty-state.compact {
    padding: 1rem;
    border-radius: 18px;
    background: #f8fafc;
    border: 1px dashed #cbd5e1;
    color: #64748b;
    text-align: center;
}

.empty-title { font-size: 1.25rem; font-weight: 800; color: #0f172a; }

/* Skeleton / shimmer para estados de carga */
.nueva-skeleton {
    height: 360px;
    border-radius: 24px;
    background: linear-gradient(90deg, #e5e7eb 25%, #f3f4f6 37%, #e5e7eb 63%);
    background-size: 400% 100%;
    animation: shimmer 1.4s ease infinite;
}

@keyframes shimmer {
    0% { background-position: 100% 0; }
    100% { background-position: 0 0; }
}
```

## 11. Cajas de aviso (notice box)

```css
.notice-box {
    padding: 0.9rem 1rem;
    border-radius: 18px;
    border: 1px solid #bfdbfe;
    background: #eff6ff;
    color: #1e3a8a;
    font-weight: 750;
}

.notice-box.success { border-color: #bbf7d0; background: #f0fdf4; color: #166534; }
.notice-box.error   { border-color: #fecaca; background: #fef2f2; color: #991b1b; }
```

## 12. Grillas de página comunes

```css
/* Página de detalle/creación: contenido principal + panel lateral */
.nueva-grid {
    display: grid;
    grid-template-columns: minmax(0, 1.35fr) minmax(320px, 0.85fr);
    gap: 1rem;
}

/* Dashboard: métricas arriba + dos columnas abajo */
.stats-grid {
    display: grid;
    grid-template-columns: repeat(4, minmax(0, 1fr));
    gap: 1rem;
}

.index-grid {
    display: grid;
    grid-template-columns: minmax(0, 0.85fr) minmax(0, 1.15fr);
    gap: 1rem;
}
```

## 13. Responsive

Breakpoint principal: **768px** (móvil) y **1200px** (grillas de 4 columnas → 2).

```css
@media (max-width: 768px) {
    .nueva-hero,
    .panel-card,
    .panel-head {
        flex-direction: column;
        align-items: flex-start;
        padding: 1rem;
    }

    .hero-actions,
    .hero-btn {
        width: 100%;
    }

    .stats-grid,
    .nueva-grid,
    .index-grid,
    .form-grid {
        grid-template-columns: 1fr;
    }
}

@media (max-width: 1200px) {
    .stats-grid {
        grid-template-columns: repeat(2, minmax(0, 1fr));
    }
}
```

## 14. Cómo portarlo a otro proyecto

1. Copia el bloque de **variables** (sección 2) al `:root` de tu hoja de estilos global.
2. Copia la estructura de **layout** (sección 3) si vas a replicar sidebar + top bar oscuros.
3. Toma solo las clases de componentes que necesites (hero, panel-card, botones, pills, tablas,
   formularios) — son independientes entre sí, no hace falta adoptar todo el sistema de una vez.
4. Todos los colores "duros" en los bloques de componentes (ej. `#0f172a`, `#64748b`) pueden
   reemplazarse por variables si tu proyecto ya tiene su propia paleta; los valores de la sección 2
   son los que usa `qcSolicitudDesviacion` como referencia por defecto.
5. No hay dependencia de JS: los estados hover/focus son puro CSS. Bootstrap 5 es opcional (solo
   se usa para utilidades de espaciado tipo `px-4`, `container-fluid`).
