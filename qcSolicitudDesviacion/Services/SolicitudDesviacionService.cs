using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Constants;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;
using qcSolicitudDesviacion.ViewModels;

namespace qcSolicitudDesviacion.Services;

public class SolicitudDesviacionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly QcDesvWorkflowService _workflowService;

    public SolicitudDesviacionService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        QcDesvWorkflowService workflowService)
    {
        _contextFactory = contextFactory;
        _workflowService = workflowService;
    }

    public async Task<ServiceResult<int>> CrearYEnviarAsync(
        QcDesvSolicitudFormModel model,
        int usuarioActualId,
        CancellationToken cancellationToken = default)
    {
        var errores = ValidarSolicitud(model);

        if (errores.Count > 0)
        {
            return ServiceResult<int>.Failure(string.Join(" ", errores));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var usuarioExiste = await context.QcDesvUsuarios
            .AnyAsync(x => x.Id == usuarioActualId && x.Activo, cancellationToken);

        if (!usuarioExiste)
        {
            return ServiceResult<int>.Failure("El usuario actual no existe o no está activo.");
        }

        if (model.UsuarioSolicitanteId <= 0)
        {
            model.UsuarioSolicitanteId = usuarioActualId;
        }

        var solicitanteExiste = await context.QcDesvUsuarios
            .AnyAsync(x => x.Id == model.UsuarioSolicitanteId && x.Activo, cancellationToken);

        if (!solicitanteExiste)
        {
            return ServiceResult<int>.Failure("El usuario solicitante no existe o no está activo.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var solicitud = new QcDesvSolicitud
            {
                UsuarioSolicitanteId = model.UsuarioSolicitanteId,
                NombreSolicitante = model.NombreSolicitante.Trim(),
                FechaSolicitud = model.FechaSolicitud.Date,

                DepartamentoSolicitante = model.DepartamentoSolicitante.Trim(),
                UnidadNegocioAfectada = model.UnidadNegocioAfectada.Trim(),
                CodigoLinea = model.CodigoLinea.Trim(),
                Clientes = model.Clientes.Trim(),

                TipoDesviacionCodigo = model.TipoDesviacionCodigo,
                FechaVigencia = model.FechaVigencia.Date,

                CantidadPiezas = model.CantidadPiezas,
                NumeroParteAfectado = model.NumeroParteAfectado.Trim(),

                RequisitoProcesoCodigo = model.RequisitoProcesoCodigo,
                RequisitoProcesoOtro = string.IsNullOrWhiteSpace(model.RequisitoProcesoOtro)
                    ? null
                    : model.RequisitoProcesoOtro.Trim(),

                CSR1 = model.CSR1?.Trim(),
                CSR2 = model.CSR2?.Trim(),
                CSR3 = model.CSR3?.Trim(),

                InformarCliente = model.InformarCliente,
                RazonInformarCliente = model.RazonInformarCliente.Trim(),

                EstadoSolicitudCodigo = QcDesvCodigos.EstadoSolicitud.Borrador,
                NumeroExtensionActual = 0,

                CreadoPorUsuarioId = usuarioActualId,
                FechaCreacion = DateTime.Now
            };

            context.QcDesvSolicitudes.Add(solicitud);
            await context.SaveChangesAsync(cancellationToken);

            var descripcion = new QcDesvDescripcion
            {
                SolicitudId = solicitud.Id,
                ComoDebeRealizarseProceso = model.ComoDebeRealizarseProceso.Trim(),
                ControlesOperacionActual = model.ControlesOperacionActual.Trim(),
                ProcesoDespuesCambioTemporal = model.ProcesoDespuesCambioTemporal.Trim(),
                ControlesDuranteCambio = model.ControlesDuranteCambio.Trim(),
                CreadoPorUsuarioId = usuarioActualId,
                FechaCreacion = DateTime.Now
            };

            context.QcDesvDescripciones.Add(descripcion);

            if (model.TipoDesviacionCodigo == QcDesvCodigos.TipoDesviacion.Critica)
            {
                var analisisRiesgo = new QcDesvAnalisisRiesgo
                {
                    SolicitudId = solicitud.Id,

                    SeveridadActual = (byte)model.SeveridadActual!.Value,
                    OcurrenciaActual = (byte)model.OcurrenciaActual!.Value,
                    DeteccionActual = (byte)model.DeteccionActual!.Value,

                    OcurrenciaConCambio = (byte)model.OcurrenciaConCambio!.Value,
                    DeteccionConCambio = (byte)model.DeteccionConCambio!.Value,

                    CreadoPorUsuarioId = usuarioActualId,
                    FechaCreacion = DateTime.Now
                };

                context.QcDesvAnalisisRiesgos.Add(analisisRiesgo);
            }

            if (!string.IsNullOrWhiteSpace(model.DescripcionAccion))
            {
                var accion = new QcDesvAccion
                {
                    SolicitudId = solicitud.Id,
                    DescripcionAccion = model.DescripcionAccion.Trim(),
                    Responsable = string.IsNullOrWhiteSpace(model.ResponsableAccion)
                        ? null
                        : model.ResponsableAccion.Trim(),
                    FechaCompromiso = model.FechaCompromisoAccion?.Date,
                    CreadoPorUsuarioId = usuarioActualId,
                    FechaCreacion = DateTime.Now
                };

                context.QcDesvAcciones.Add(accion);
            }

            var workflowResult = await _workflowService.CrearAprobacionesAsync(
                context,
                solicitud.Id,
                extensionId: null,
                tipoFlujoCodigo: QcDesvCodigos.TipoFlujo.Solicitud,
                cancellationToken);

            if (!workflowResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<int>.Failure(workflowResult.Message);
            }

            solicitud.EstadoSolicitudCodigo = workflowResult.Data!;

            context.QcDesvHistoriales.Add(new QcDesvHistorial
            {
                SolicitudId = solicitud.Id,
                Accion = "Solicitud creada y enviada",
                EstadoAnteriorCodigo = QcDesvCodigos.EstadoSolicitud.Borrador,
                EstadoNuevoCodigo = solicitud.EstadoSolicitudCodigo,
                Comentario = "La solicitud fue creada y enviada al flujo de aprobación.",
                UsuarioId = usuarioActualId,
                FechaMovimiento = DateTime.Now
            });

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<int>.Success(solicitud.Id, "Solicitud creada y enviada correctamente.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<int>.Failure($"Error al crear la solicitud: {ex.Message}");
        }
    }

    public async Task<List<QcDesvSolicitudResumen>> ObtenerResumenAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.QcDesvSolicitudesResumen
            .AsNoTracking()
            .OrderByDescending(x => x.SolicitudId)
            .ToListAsync(cancellationToken);
    }

    public async Task<QcDesvSolicitud?> ObtenerDetalleAsync(
    int solicitudId,
    CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.QcDesvSolicitudes
            .AsNoTracking()
            .Include(x => x.TipoDesviacion)
            .Include(x => x.RequisitoProceso)
            .Include(x => x.EstadoSolicitud)
            .Include(x => x.Descripcion)
            .Include(x => x.AnalisisRiesgo)
            .Include(x => x.Acciones)
                .ThenInclude(x => x.Archivos)
            .Include(x => x.Archivos)
            .Include(x => x.Extensiones)
            .Include(x => x.Aprobaciones)
                .ThenInclude(x => x.RolAprobador)
            .Include(x => x.Aprobaciones)
                .ThenInclude(x => x.EstadoAprobacion)
            .Include(x => x.Aprobaciones)
                .ThenInclude(x => x.UsuarioAprobador)
            .Include(x => x.Control)
            .Include(x => x.Historiales)
            .FirstOrDefaultAsync(x => x.Id == solicitudId, cancellationToken);
    }

    public async Task<ServiceResult<QcDesvSolicitudFormModel>> ObtenerParaEditarAsync(
    int solicitudId,
    int usuarioActualId,
    CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var solicitud = await context.QcDesvSolicitudes
            .AsNoTracking()
            .Include(x => x.Descripcion)
            .Include(x => x.AnalisisRiesgo)
            .Include(x => x.Acciones)
            .FirstOrDefaultAsync(x => x.Id == solicitudId, cancellationToken);

        if (solicitud is null)
        {
            return ServiceResult<QcDesvSolicitudFormModel>.Failure("La solicitud no existe.");
        }

        if (solicitud.UsuarioSolicitanteId != usuarioActualId)
        {
            return ServiceResult<QcDesvSolicitudFormModel>.Failure("Solo el solicitante original puede editar esta solicitud.");
        }

        if (!PuedeEditarSolicitud(solicitud.EstadoSolicitudCodigo))
        {
            return ServiceResult<QcDesvSolicitudFormModel>.Failure("Solo se pueden editar solicitudes en borrador o rechazadas.");
        }

        var accion = solicitud.Acciones
            .OrderBy(x => x.Id)
            .FirstOrDefault();

        var model = new QcDesvSolicitudFormModel
        {
            SolicitudId = solicitud.Id,

            UsuarioSolicitanteId = solicitud.UsuarioSolicitanteId,
            NombreSolicitante = solicitud.NombreSolicitante,
            FechaSolicitud = solicitud.FechaSolicitud,

            DepartamentoSolicitante = solicitud.DepartamentoSolicitante,
            UnidadNegocioAfectada = solicitud.UnidadNegocioAfectada,
            CodigoLinea = solicitud.CodigoLinea,
            Clientes = solicitud.Clientes,

            TipoDesviacionCodigo = solicitud.TipoDesviacionCodigo,
            FechaVigencia = solicitud.FechaVigencia,

            CantidadPiezas = solicitud.CantidadPiezas,
            NumeroParteAfectado = solicitud.NumeroParteAfectado,

            RequisitoProcesoCodigo = solicitud.RequisitoProcesoCodigo,
            RequisitoProcesoOtro = solicitud.RequisitoProcesoOtro,

            CSR1 = solicitud.CSR1,
            CSR2 = solicitud.CSR2,
            CSR3 = solicitud.CSR3,

            InformarCliente = solicitud.InformarCliente,
            RazonInformarCliente = solicitud.RazonInformarCliente,

            ComoDebeRealizarseProceso = solicitud.Descripcion?.ComoDebeRealizarseProceso ?? string.Empty,
            ControlesOperacionActual = solicitud.Descripcion?.ControlesOperacionActual ?? string.Empty,
            ProcesoDespuesCambioTemporal = solicitud.Descripcion?.ProcesoDespuesCambioTemporal ?? string.Empty,
            ControlesDuranteCambio = solicitud.Descripcion?.ControlesDuranteCambio ?? string.Empty,

            SeveridadActual = solicitud.AnalisisRiesgo is null ? null : solicitud.AnalisisRiesgo.SeveridadActual,
            OcurrenciaActual = solicitud.AnalisisRiesgo is null ? null : solicitud.AnalisisRiesgo.OcurrenciaActual,
            DeteccionActual = solicitud.AnalisisRiesgo is null ? null : solicitud.AnalisisRiesgo.DeteccionActual,

            OcurrenciaConCambio = solicitud.AnalisisRiesgo is null ? null : solicitud.AnalisisRiesgo.OcurrenciaConCambio,
            DeteccionConCambio = solicitud.AnalisisRiesgo is null ? null : solicitud.AnalisisRiesgo.DeteccionConCambio,

            DescripcionAccion = accion?.DescripcionAccion ?? string.Empty,
            ResponsableAccion = accion?.Responsable,
            FechaCompromisoAccion = accion?.FechaCompromiso
        };

        return ServiceResult<QcDesvSolicitudFormModel>.Success(model);
    }

    public async Task<ServiceResult<int>> ActualizarYReenviarAsync(
        QcDesvSolicitudFormModel model,
        int usuarioActualId,
        CancellationToken cancellationToken = default)
    {
        if (model.SolicitudId <= 0)
        {
            return ServiceResult<int>.Failure("La solicitud es obligatoria.");
        }

        var errores = ValidarSolicitud(model);

        if (errores.Count > 0)
        {
            return ServiceResult<int>.Failure(string.Join(" ", errores));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var solicitud = await context.QcDesvSolicitudes
            .Include(x => x.Descripcion)
            .Include(x => x.AnalisisRiesgo)
            .Include(x => x.Acciones)
            .FirstOrDefaultAsync(x => x.Id == model.SolicitudId, cancellationToken);

        if (solicitud is null)
        {
            return ServiceResult<int>.Failure("La solicitud no existe.");
        }

        if (solicitud.UsuarioSolicitanteId != usuarioActualId)
        {
            return ServiceResult<int>.Failure("Solo el solicitante original puede editar esta solicitud.");
        }

        if (!PuedeEditarSolicitud(solicitud.EstadoSolicitudCodigo))
        {
            return ServiceResult<int>.Failure("Solo se pueden editar y reenviar solicitudes en borrador o rechazadas.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var estadoAnterior = solicitud.EstadoSolicitudCodigo;

            solicitud.NombreSolicitante = model.NombreSolicitante.Trim();
            solicitud.FechaSolicitud = model.FechaSolicitud.Date;

            solicitud.DepartamentoSolicitante = model.DepartamentoSolicitante.Trim();
            solicitud.UnidadNegocioAfectada = model.UnidadNegocioAfectada.Trim();
            solicitud.CodigoLinea = model.CodigoLinea.Trim();
            solicitud.Clientes = model.Clientes.Trim();

            solicitud.TipoDesviacionCodigo = model.TipoDesviacionCodigo;
            solicitud.FechaVigencia = model.FechaVigencia.Date;

            solicitud.CantidadPiezas = model.CantidadPiezas;
            solicitud.NumeroParteAfectado = model.NumeroParteAfectado.Trim();

            solicitud.RequisitoProcesoCodigo = model.RequisitoProcesoCodigo;
            solicitud.RequisitoProcesoOtro = string.IsNullOrWhiteSpace(model.RequisitoProcesoOtro)
                ? null
                : model.RequisitoProcesoOtro.Trim();

            solicitud.CSR1 = model.CSR1?.Trim();
            solicitud.CSR2 = model.CSR2?.Trim();
            solicitud.CSR3 = model.CSR3?.Trim();

            solicitud.InformarCliente = model.InformarCliente;
            solicitud.RazonInformarCliente = model.RazonInformarCliente.Trim();

            solicitud.ModificadoPorUsuarioId = usuarioActualId;
            solicitud.FechaModificacion = DateTime.Now;

            if (solicitud.Descripcion is null)
            {
                solicitud.Descripcion = new QcDesvDescripcion
                {
                    SolicitudId = solicitud.Id,
                    CreadoPorUsuarioId = usuarioActualId,
                    FechaCreacion = DateTime.Now
                };

                context.QcDesvDescripciones.Add(solicitud.Descripcion);
            }

            solicitud.Descripcion.ComoDebeRealizarseProceso = model.ComoDebeRealizarseProceso.Trim();
            solicitud.Descripcion.ControlesOperacionActual = model.ControlesOperacionActual.Trim();
            solicitud.Descripcion.ProcesoDespuesCambioTemporal = model.ProcesoDespuesCambioTemporal.Trim();
            solicitud.Descripcion.ControlesDuranteCambio = model.ControlesDuranteCambio.Trim();
            solicitud.Descripcion.ModificadoPorUsuarioId = usuarioActualId;
            solicitud.Descripcion.FechaModificacion = DateTime.Now;

            if (model.TipoDesviacionCodigo == QcDesvCodigos.TipoDesviacion.Critica)
            {
                if (solicitud.AnalisisRiesgo is null)
                {
                    solicitud.AnalisisRiesgo = new QcDesvAnalisisRiesgo
                    {
                        SolicitudId = solicitud.Id,
                        CreadoPorUsuarioId = usuarioActualId,
                        FechaCreacion = DateTime.Now
                    };

                    context.QcDesvAnalisisRiesgos.Add(solicitud.AnalisisRiesgo);
                }

                solicitud.AnalisisRiesgo.SeveridadActual = (byte)model.SeveridadActual!.Value;
                solicitud.AnalisisRiesgo.OcurrenciaActual = (byte)model.OcurrenciaActual!.Value;
                solicitud.AnalisisRiesgo.DeteccionActual = (byte)model.DeteccionActual!.Value;
                solicitud.AnalisisRiesgo.OcurrenciaConCambio = (byte)model.OcurrenciaConCambio!.Value;
                solicitud.AnalisisRiesgo.DeteccionConCambio = (byte)model.DeteccionConCambio!.Value;
                solicitud.AnalisisRiesgo.ModificadoPorUsuarioId = usuarioActualId;
                solicitud.AnalisisRiesgo.FechaModificacion = DateTime.Now;
            }
            else
            {
                if (solicitud.AnalisisRiesgo is not null)
                {
                    context.QcDesvAnalisisRiesgos.Remove(solicitud.AnalisisRiesgo);
                }
            }

            var accion = solicitud.Acciones
                .OrderBy(x => x.Id)
                .FirstOrDefault();

            if (accion is null)
            {
                accion = new QcDesvAccion
                {
                    SolicitudId = solicitud.Id,
                    CreadoPorUsuarioId = usuarioActualId,
                    FechaCreacion = DateTime.Now
                };

                context.QcDesvAcciones.Add(accion);
            }

            accion.DescripcionAccion = model.DescripcionAccion.Trim();
            accion.Responsable = string.IsNullOrWhiteSpace(model.ResponsableAccion)
                ? null
                : model.ResponsableAccion.Trim();
            accion.FechaCompromiso = model.FechaCompromisoAccion?.Date;
            accion.ModificadoPorUsuarioId = usuarioActualId;
            accion.FechaModificacion = DateTime.Now;

            var aprobacionesAnteriores = await context.QcDesvAprobaciones
                .Where(x => x.SolicitudId == solicitud.Id && x.ExtensionId == null)
                .ToListAsync(cancellationToken);

            context.QcDesvAprobaciones.RemoveRange(aprobacionesAnteriores);

            await context.SaveChangesAsync(cancellationToken);

            var workflowResult = await _workflowService.CrearAprobacionesAsync(
                context,
                solicitud.Id,
                extensionId: null,
                tipoFlujoCodigo: QcDesvCodigos.TipoFlujo.Solicitud,
                cancellationToken);

            if (!workflowResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<int>.Failure(workflowResult.Message);
            }

            solicitud.EstadoSolicitudCodigo = workflowResult.Data!;

            context.QcDesvHistoriales.Add(new QcDesvHistorial
            {
                SolicitudId = solicitud.Id,
                Accion = "Solicitud actualizada y reenviada",
                EstadoAnteriorCodigo = estadoAnterior,
                EstadoNuevoCodigo = solicitud.EstadoSolicitudCodigo,
                Comentario = "La solicitud fue actualizada por el solicitante y reenviada al flujo de aprobación.",
                UsuarioId = usuarioActualId,
                FechaMovimiento = DateTime.Now
            });

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<int>.Success(solicitud.Id, "Solicitud actualizada y reenviada correctamente.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<int>.Failure($"Error al actualizar la solicitud: {ex.Message}");
        }
    }

    private static bool PuedeEditarSolicitud(string estadoSolicitudCodigo)
    {
        return estadoSolicitudCodigo == QcDesvCodigos.EstadoSolicitud.Borrador ||
               estadoSolicitudCodigo == QcDesvCodigos.EstadoSolicitud.Rechazada;
    }

    public async Task<ServiceResult> CancelarAsync(
        int solicitudId,
        int usuarioActualId,
        string comentario,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var solicitud = await context.QcDesvSolicitudes
            .FirstOrDefaultAsync(x => x.Id == solicitudId, cancellationToken);

        if (solicitud is null)
        {
            return ServiceResult.Failure("La solicitud no existe.");
        }

        if (solicitud.UsuarioSolicitanteId != usuarioActualId)
        {
            return ServiceResult.Failure("Solo el solicitante puede cancelar la solicitud.");
        }

        if (solicitud.EstadoSolicitudCodigo == QcDesvCodigos.EstadoSolicitud.Cerrada)
        {
            return ServiceResult.Failure("No se puede cancelar una desviación cerrada.");
        }

        var estadoAnterior = solicitud.EstadoSolicitudCodigo;

        solicitud.EstadoSolicitudCodigo = QcDesvCodigos.EstadoSolicitud.Cancelada;
        solicitud.ModificadoPorUsuarioId = usuarioActualId;
        solicitud.FechaModificacion = DateTime.Now;

        context.QcDesvHistoriales.Add(new QcDesvHistorial
        {
            SolicitudId = solicitud.Id,
            Accion = "Solicitud cancelada",
            EstadoAnteriorCodigo = estadoAnterior,
            EstadoNuevoCodigo = QcDesvCodigos.EstadoSolicitud.Cancelada,
            Comentario = comentario,
            UsuarioId = usuarioActualId,
            FechaMovimiento = DateTime.Now
        });

        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success("Solicitud cancelada correctamente.");
    }

    private static List<string> ValidarSolicitud(QcDesvSolicitudFormModel model)
    {
        var errores = new List<string>();

        if (model.UsuarioSolicitanteId <= 0)
            errores.Add("El usuario solicitante es obligatorio.");

        if (string.IsNullOrWhiteSpace(model.NombreSolicitante))
            errores.Add("El nombre del solicitante es obligatorio.");

        if (string.IsNullOrWhiteSpace(model.DepartamentoSolicitante))
            errores.Add("El departamento solicitante es obligatorio.");

        if (string.IsNullOrWhiteSpace(model.UnidadNegocioAfectada))
            errores.Add("La unidad de negocio afectada es obligatoria.");

        if (string.IsNullOrWhiteSpace(model.CodigoLinea))
            errores.Add("El código de la línea es obligatorio.");

        if (string.IsNullOrWhiteSpace(model.Clientes))
            errores.Add("El cliente es obligatorio.");

        if (model.TipoDesviacionCodigo != QcDesvCodigos.TipoDesviacion.Critica &&
            model.TipoDesviacionCodigo != QcDesvCodigos.TipoDesviacion.Estandar)
        {
            errores.Add("El tipo de desviación no es válido.");
        }

        if (model.FechaVigencia.Date < model.FechaSolicitud.Date)
            errores.Add("La fecha de vigencia no puede ser menor a la fecha de solicitud.");

        if (model.CantidadPiezas.HasValue && model.CantidadPiezas.Value <= 0)
        {
            errores.Add("La cantidad de piezas debe ser mayor a 0.");
        }

        if (string.IsNullOrWhiteSpace(model.NumeroParteAfectado))
            errores.Add("El número de parte afectado es obligatorio.");

        if (string.IsNullOrWhiteSpace(model.RequisitoProcesoCodigo))
            errores.Add("El requisito del proceso es obligatorio.");

        if (model.RequisitoProcesoCodigo == QcDesvCodigos.RequisitoProceso.Otro &&
            string.IsNullOrWhiteSpace(model.RequisitoProcesoOtro))
        {
            errores.Add("Debe especificar el requisito del proceso cuando selecciona Otro.");
        }

        if (string.IsNullOrWhiteSpace(model.CSR1))
            errores.Add("El CSR 1 es obligatorio.");

        if (string.IsNullOrWhiteSpace(model.CSR2))
            errores.Add("El CSR 2 es obligatorio.");

        if (string.IsNullOrWhiteSpace(model.CSR3))
            errores.Add("El CSR 3 es obligatorio.");

        if (model.InformarCliente && string.IsNullOrWhiteSpace(model.RazonInformarCliente))
            errores.Add("Debe capturar la razón para informar al cliente.");

        if (string.IsNullOrWhiteSpace(model.ComoDebeRealizarseProceso))
            errores.Add("Debe capturar cómo debe realizarse el proceso.");

        if (string.IsNullOrWhiteSpace(model.ControlesOperacionActual))
            errores.Add("Debe capturar los controles de la operación actual.");

        if (string.IsNullOrWhiteSpace(model.ProcesoDespuesCambioTemporal))
            errores.Add("Debe capturar cómo se ejecutará el proceso con el cambio temporal.");

        if (string.IsNullOrWhiteSpace(model.ControlesDuranteCambio))
            errores.Add("Debe capturar los controles durante la operación con cambio.");

        if (model.TipoDesviacionCodigo == QcDesvCodigos.TipoDesviacion.Critica)
        {
            if (ValorAmefInvalido(model.SeveridadActual))
                errores.Add("La severidad debe estar entre 1 y 10.");

            if (ValorAmefInvalido(model.OcurrenciaActual))
                errores.Add("La ocurrencia actual debe estar entre 1 y 10.");

            if (ValorAmefInvalido(model.DeteccionActual))
                errores.Add("La detección actual debe estar entre 1 y 10.");

            if (ValorAmefInvalido(model.OcurrenciaConCambio))
                errores.Add("La ocurrencia con cambio debe estar entre 1 y 10.");

            if (ValorAmefInvalido(model.DeteccionConCambio))
                errores.Add("La detección con cambio debe estar entre 1 y 10.");
        }

        

        return errores;
    }

    private static bool ValorAmefInvalido(int? valor)
    {
        return !valor.HasValue || valor.Value < 1 || valor.Value > 10;
    }
}