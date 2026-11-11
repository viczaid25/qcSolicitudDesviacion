# Autenticación

Dos modos, seleccionados en `Program.cs` una sola vez al arrancar:

```csharp
var devLoginActivo = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Auth:DevLogin");
```

**Doble candado**: en IIS el `Environment` siempre es `Production`, así que el modo desarrollo es
imposible ahí aunque `Auth:DevLogin=true` se copie por error a producción.

Ambos modos emiten exactamente el mismo conjunto de claims (`MeaxClaims.CrearIdentidad`), así que el
resto de la aplicación (`UsuarioActualService`, todas las páginas) **no tiene ni un solo `if` de
modo** — les da igual cómo se autenticó el usuario.

## Modo desarrollo (`Auth/DevAuthExtensions.cs` + `Pages/Account/Login.cshtml`)

- Cookie auth simple, sin JWT.
- `/Account/Login`: usuario libre (`PcLoginId`) + contraseña fija (`Auth:DevPassword`,
  default `"dev"`).
- Si el usuario tecleado coincide con `Auth:DevUser:PcLoginId` (appsettings.Development.json), se
  usa su perfil completo (nombre, departamento, puesto, nómina, `SystemRole`) de esa sección. Con
  cualquier otro usuario, se conserva el `SystemRole` de config pero sin nómina/puesto (evita falsos
  positivos contra la tabla de RR.HH.).
- Aprovisiona el usuario local igual que el flujo de producción (`UsuarioProvisioningService`).
- Cookie `qcSolicitudDesviacion.Auth`, 8 horas, `SlidingExpiration`.

## Modo producción — SSO de meaxHub (`Auth/HubAuthExtensions.cs`)

- JWT **HS256**, firmado con `Jwt:Secret` — **debe ser idéntico byte a byte** al del hub, issuer
  `meaxHub`, audience `meax-services` (configurables en `Jwt:Issuer`/`Jwt:Audience`).
- El token llega por: cookie `.MEAX.JWT` (mismo host que el hub) **o** query string
  `?meax_token=...` (deep-link SSO, vida útil ~5 min).
- `AddPolicyScheme`: si la request trae cookie del hub o el query param, usa el esquema JWT; si no,
  usa la cookie local propia de la app.
- Al validar el token (`OnTokenValidated`): resuelve `PcLoginId` (o `sub`), aprovisiona/actualiza el
  usuario local vía `UsuarioProvisioningService`, y cachea el resultado 10 minutos
  (`IMemoryCache`, clave `meax-usuario:{login}`) para no tocar la BD en cada request.
- `OnChallenge`: **nunca responde 401** — redirige a
  `{Hub:BaseUrl}/auth/sso?system={Hub:SystemCode}&returnUrl={url actual sin token}`.
- **Puente SSO** (`UseHubSsoTokenBridge`, middleware): cuando una request GET trae `?meax_token=` y
  ya quedó autenticada, convierte esos claims en una **cookie de sesión local persistente** y
  redirige a la misma URL sin el token — así el circuito de Blazor Server (que vive más que los 5
  minutos del token) no depende de un JWT efímero.
- `PathBase` se respeta en todas las URLs de retorno (`UriHelper.BuildAbsolute`) porque la app corre
  como sub-aplicación de IIS bajo `meax.one`.

## Contrato de claims (`Auth/MeaxClaims.cs`)

| Claim | Origen | Uso |
|---|---|---|
| `PcLoginId`, `DisplayName`, `Department`, `Position`, `EmployeeId`, `SystemRole` | JWT del hub / `Auth:DevUser` | Datos crudos del usuario |
| `qc_usuario_id` | Generado al aprovisionar | `UsuarioActualService` lo usa como fuente principal del Id local |
| `pc_login_id`, `username` | Generado al aprovisionar | Login normalizado, usado para el lookup de departamento contra `meax_all_user` |
| `ClaimTypes.NameIdentifier` / `.Name` / `.Email` | Estándar .NET | Compatibilidad con `[Authorize]`, `User.Identity.Name`, etc. |

`UsuarioActualService.ObtenerUsuarioIdAsync()` lee `qc_usuario_id` (o cae a
`ClaimTypes.NameIdentifier`) y lo cachea en memoria por instancia del servicio — es la única forma
correcta de saber "quién es el usuario actual" en cualquier página/servicio.

## Alta automática de usuarios

`UsuarioProvisioningService.ObtenerOCrearUsuarioAsync`: si el `UserName` (login normalizado a
minúsculas) no existe en `qcDesvUsuario`, lo crea con `Email` placeholder
(`{login}@sin-correo.local`, porque el JWT del hub no trae correo) y le asigna el rol `SOLICITANTE`
por defecto. En cada login refresca `NombreCompleto` y `Departamento` (este último, si es posible,
tomado de `meax_all_user` en vez del claim del JWT — es más confiable). El correo real, una vez
puesto, nunca se sobreescribe automáticamente.
