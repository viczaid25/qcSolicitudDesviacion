# Despliegue y operación

## Hosting

- IIS, sub-aplicación bajo `meax.one` (ej. ruta física `C:\inetpub\Services\Desviaciones\`).
- `App.razor` calcula `BaseHref` a partir de `HttpContext.Request.PathBase`, así que la app funciona
  tanto en la raíz de un sitio como en una sub-aplicación sin cambiar código.
- Al publicar (copiar archivos nuevos a la carpeta del sitio), **IIS recicla el Application Pool**
  automáticamente — esto mata cualquier circuito de Blazor Server activo. Cualquiera con una pestaña
  abierta en ese momento ve **"Attempting to reconnect to the server"**; es esperado, no un bug.
  Basta con recargar la página (F5) una vez que el nuevo proceso esté arriba.

## Configuración (`appsettings.json` / `appsettings.Development.json`)

| Sección | Contenido |
|---|---|
| `ConnectionStrings:DefaultConnection` | Cadena SQL Server completa (⚠️ contraseña en texto plano, ver nota de seguridad abajo) |
| `Jwt:Secret/Issuer/Audience` | Debe coincidir exactamente con la config del hub |
| `Hub:BaseUrl/SystemCode` | URL del hub y código de sistema (`DES`) para el redirect de SSO |
| `EmailSettings:*` | SMTP, remitente, `AppBaseUrl` (base para los links de los correos — debe ser la URL pública real, no una IP interna), `QmsNotificationEmail`, `TestOverrideEmail` (solo dev: redirige *todos* los correos a una sola dirección de prueba) |
| `Auth:DevLogin/DevPassword/DevUser` | Solo en `appsettings.Development.json`; ignorado en producción por el doble candado de `Program.cs` |

### ⚠️ Nota de seguridad pendiente

La contraseña de SQL Server vive en texto plano dentro de `appsettings.json`, versionado en git.
Migrar a un almacén de secretos (variable de entorno de IIS, Azure Key Vault, `dotnet user-secrets`
en dev) es una mejora recomendada, no aplicada todavía por decisión explícita de alcance.

## Requisitos del servidor (permisos de archivo)

`ArchivoService` escribe adjuntos en `wwwroot/uploads/qcDesv/{SolicitudId}/`. **El identity del
Application Pool de IIS necesita permiso de escritura (Modify) sobre esa carpeta** — no viene dado
por defecto. Para otorgarlo (PowerShell, como Administrador, en el servidor):

```powershell
$folder = "C:\inetpub\Services\Desviaciones\wwwroot\uploads"
New-Item -ItemType Directory -Path $folder -Force
icacls $folder /grant "IIS AppPool\NOMBRE_DEL_POOL:(OI)(CI)M" /T
```

## Checklist al publicar

1. Publicar los archivos nuevos.
2. Confirmar que el Application Pool se recicló (o reciclarlo manualmente) para tomar el código nuevo.
3. Si el cambio toca subida de archivos, correo, o el flujo de aprobación: crear una solicitud de
   prueba con un adjunto pequeño y confirmar que aparece en `Detalle.razor` antes de dar por bueno el
   despliegue.
4. Avisar a quien tenga una pestaña abierta que puede ver el mensaje de reconexión — es normal.

## Incidentes conocidos y sus arreglos (histórico, por si se repiten)

### 1. Límite de mensaje de SignalR bloqueaba TODOS los adjuntos (32 KB)

Blazor Server transmite los archivos de `IBrowserFile` como mensajes de SignalR. El límite por
defecto (`HubOptions.MaximumReceiveMessageSize`) es **32 KB** — cualquier foto o PDF real lo excede
y la subida se corta a nivel de transporte, sin importar el límite de 20 MB que valida la propia
app. Arreglo en `Program.cs`:

```csharp
builder.Services.Configure<HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 25 * 1024 * 1024;
});
```

### 2. Notificación por correo después del commit tumbaba el circuito completo

En `SolicitudDesviacionService.CrearYEnviarAsync`, `AprobacionService.AprobarAsync`/`RechazarAsync`
y `ExtensionService`, el envío de la notificación ocurría **dentro** del mismo bloque `try` cuyo
`catch` hacía `transaction.RollbackAsync(...)` — pero la transacción ya se había confirmado
(`CommitAsync`) antes de llegar ahí. Si el correo tardaba o fallaba, el `catch` intentaba un
rollback sobre una transacción ya cerrada, lo cual lanza una **segunda excepción sin capturar** que
tumba todo el circuito de Blazor Server ("unhandled exception on the circuit"). Arreglo: mover la
llamada de notificación **fuera** del `try/catch` transaccional en los 4 métodos, y agregar
`Timeout = 10_000` al `SmtpClient` en `EmailNotificationService` para que un servidor de correo
lento/inalcanzable falle rápido en vez de colgar la respuesta.

### 3. Permiso de escritura en `wwwroot/uploads` (IIS)

Ver sección de permisos arriba. Sin ese permiso, `Directory.CreateDirectory` lanzaba
`UnauthorizedAccessException` **fuera** de cualquier try/catch en `ArchivoService`, con el mismo
efecto de tumbar el circuito. Arreglo: se movió esa línea dentro del try/catch existente del método,
así que ahora cualquier falla de disco se reporta como un error normal ("no se pudo subir el
archivo") en vez de romper la sesión — pero el **permiso real en el servidor sigue siendo
necesario** para que la subida funcione de verdad.

### 4. `Editar.razor` no tenía forma de adjuntar archivos

Al corregir una solicitud rechazada (p. ej. por falta de evidencia) no había ningún campo de
adjuntos en esa pantalla — se agregó, con la misma validación de "si hay descripción de acciones,
debe haber al menos un archivo" que ya existía en `Crear.razor`.

### 5. Badge de vigencia mostraba "Por vencer" en solicitudes aún en revisión

`Desviaciones.razor` calculaba Vigente/Por vencer/Vencida para cualquier solicitud no
cerrada/cancelada/rechazada, sin importar si ya estaba aprobada. Una solicitud en revisión con
`FechaVigencia` cercana aparecía como "Por vencer" antes siquiera de ser aprobada. Arreglo: el
cálculo de vigencia ahora solo aplica cuando el estado es `APROBADA`/`EXTENSION_APROBADA`/`CERRADA`;
mientras esté en cualquier paso de revisión muestra **"En proceso / In process"**.

### 6. Identity reseed de `qcDesvSolicitud` (+999 dos veces)

Ver [02-BASE-DE-DATOS.md](02-BASE-DE-DATOS.md#incidente-conocido-identity-reseed-en-qcdesvsolicitud).
Causa raíz: linked server `SRV_ORIGEN` apuntando al servidor viejo decomisionado — ya eliminado.
