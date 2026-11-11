# Documentación técnica — Solicitud de Desviación (qcSolicitudDesviacion)

Sistema interno de MEAX para gestionar el ciclo de vida completo de una **desviación de calidad**
(deviation request): captura, flujo de aprobación multinivel, extensiones de vigencia, cierre con
datos de control, y notificaciones por correo. Integrado a **meaxHub / MEAX One** como aplicación
satélite bajo SSO.

## Índice

1. [Arquitectura](01-ARQUITECTURA.md) — stack, estructura del proyecto, rutas, capa de servicios.
2. [Base de datos](02-BASE-DE-DATOS.md) — esquema completo, relaciones, constraints, triggers, vista de resumen.
3. [Flujo de aprobación](03-FLUJO-APROBACION.md) — estados, roles, caso especial QMS, extensiones, cierre.
4. [Autenticación](04-AUTENTICACION.md) — modo desarrollo vs. SSO de meaxHub (JWT).
5. [Despliegue y operación](05-DESPLIEGUE-Y-OPERACION.md) — IIS, configuración, incidentes conocidos y sus arreglos.

En la raíz del repo, [`DESIGN_SYSTEM.md`](../DESIGN_SYSTEM.md) documenta el sistema visual **MEAX One**
(clases `mo-*`) que usa toda la interfaz.

## Resumen de una línea

Blazor Server (.NET 8) + Entity Framework Core sobre SQL Server, con render mode interactivo,
autenticación híbrida (cookie de desarrollo o JWT del hub), y un flujo de aprobación configurable
por tabla (no hardcodeado) que resuelve el paso QMS por rol `ADMIN` en vez de por persona asignada.
