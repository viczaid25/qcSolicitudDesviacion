using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;
using qcSolicitudDesviacion.Localization;

namespace qcSolicitudDesviacion.Services;

public class ArchivoService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IWebHostEnvironment _environment;

    public ArchivoService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IWebHostEnvironment environment)
    {
        _contextFactory = contextFactory;
        _environment = environment;
    }

    public async Task<ServiceResult<int>> SubirArchivoSolicitudAsync(
        int solicitudId,
        IBrowserFile archivo,
        int usuarioActualId,
        int? accionId = null,
        long maxFileSizeBytes = 20 * 1024 * 1024,
        CancellationToken cancellationToken = default)
    {
        if (solicitudId <= 0)
        {
            return ServiceResult<int>.Failure(Loc.T("The request is required."));
        }

        if (archivo is null)
        {
            return ServiceResult<int>.Failure(Loc.T("The file is required."));
        }

        if (archivo.Size <= 0)
        {
            return ServiceResult<int>.Failure(Loc.T("The file is empty."));
        }

        if (archivo.Size > maxFileSizeBytes)
        {
            return ServiceResult<int>.Failure(Loc.T("The file exceeds the maximum allowed size."));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var solicitudExiste = await context.QcDesvSolicitudes
            .AnyAsync(x => x.Id == solicitudId, cancellationToken);

        if (!solicitudExiste)
        {
            return ServiceResult<int>.Failure(Loc.T("The request does not exist."));
        }

        if (accionId.HasValue)
        {
            var accionPerteneceSolicitud = await context.QcDesvAcciones
                .AnyAsync(x => x.Id == accionId.Value && x.SolicitudId == solicitudId, cancellationToken);

            if (!accionPerteneceSolicitud)
            {
                return ServiceResult<int>.Failure(Loc.T("The action does not belong to the specified request."));
            }
        }

        var webRootPath = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        var relativeFolder = Path.Combine("uploads", "qcDesv", solicitudId.ToString());
        var absoluteFolder = Path.Combine(webRootPath, relativeFolder);

        var originalFileName = Path.GetFileName(archivo.Name);
        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(absoluteFolder, storedFileName);

        try
        {
            // Dentro del try: si el identity de la app no tiene permiso de
            // escritura en wwwroot/uploads (o el disco falla por cualquier
            // otra razón), esto debe reportarse como un Failure normal, no
            // escaparse sin capturar y tumbar el circuito de Blazor Server.
            Directory.CreateDirectory(absoluteFolder);

            await using var fileStream = new FileStream(fullPath, FileMode.CreateNew);

            await archivo.OpenReadStream(maxFileSizeBytes)
                .CopyToAsync(fileStream, cancellationToken);

            var relativePath = "/" + Path.Combine(relativeFolder, storedFileName)
                .Replace("\\", "/");

            var archivoDb = new QcDesvArchivo
            {
                SolicitudId = solicitudId,
                AccionId = accionId,
                NombreOriginal = originalFileName,
                NombreGuardado = storedFileName,
                RutaRelativa = relativePath,
                ContentType = archivo.ContentType,
                TamanoBytes = archivo.Size,
                SubidoPorUsuarioId = usuarioActualId,
                FechaSubida = DateTime.Now
            };

            context.QcDesvArchivos.Add(archivoDb);
            await context.SaveChangesAsync(cancellationToken);

            return ServiceResult<int>.Success(archivoDb.Id, Loc.T("File uploaded successfully."));
        }
        catch (Exception ex)
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            return ServiceResult<int>.Failure(Loc.T("Error uploading file: {0}", ex.Message));
        }
    }

    public async Task<ServiceResult> EliminarArchivoAsync(
        int archivoId,
        int usuarioActualId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var archivo = await context.QcDesvArchivos
            .FirstOrDefaultAsync(x => x.Id == archivoId, cancellationToken);

        if (archivo is null)
        {
            return ServiceResult.Failure(Loc.T("The file does not exist."));
        }

        var webRootPath = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        var relativePath = archivo.RutaRelativa
            .TrimStart('/')
            .Replace("/", Path.DirectorySeparatorChar.ToString());

        var fullPath = Path.Combine(webRootPath, relativePath);

        try
        {
            context.QcDesvArchivos.Remove(archivo);
            await context.SaveChangesAsync(cancellationToken);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            return ServiceResult.Success(Loc.T("File deleted successfully."));
        }
        catch (Exception ex)
        {
            return ServiceResult.Failure(Loc.T("Error deleting file: {0}", ex.Message));
        }
    }
}