using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Constants;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;
using qcSolicitudDesviacion.ViewModels;
using qcSolicitudDesviacion.Localization;

namespace qcSolicitudDesviacion.Services;

public class SolicitudDesviacionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly QcDesvWorkflowService _workflowService;
    private readonly EmailNotificationService _emailService;

    // QMS ya no requiere elegir un aprobador específico: lo atiende
    // cualquier usuario con rol Admin (ver AprobacionService).
    private static readonly string[] RolesAprobacionSolicitud =
    {
        QcDesvCodigos.Rol.CoordApqp,
        QcDesvCodigos.Rol.CoordQc,
        QcDesvCodigos.Rol.ManagerQc,
        QcDesvCodigos.Rol.ManagerProdIng,
        QcDesvCodigos.Rol.AdvisorQc
    };

    public SolicitudDesviacionService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        QcDesvWorkflowService workflowService,
        EmailNotificationService emailService)
    {
        _contextFactory = contextFactory;
        _workflowService = workflowService;
        _emailService = emailService;
    }

    public async Task<ServiceResult<int>> CrearYEnviarAsync(
        QcDesvSolicitudFormModel model,
        int usuarioActualId,
        CancellationToken cancellationToken = default)
    {
        var csrs = ObtenerCsrsNormalizados(model);
        SincronizarCsrsCompatibilidad(model, csrs);

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
            return ServiceResult<int>.Failure(Loc.T("The current user does not exist or is not active."));
        }

        if (model.UsuarioSolicitanteId <= 0)
        {
            model.UsuarioSolicitanteId = usuarioActualId;
        }

        var solicitanteExiste = await context.QcDesvUsuarios
            .AnyAsync(x => x.Id == model.UsuarioSolicitanteId && x.Activo, cancellationToken);

        if (!solicitanteExiste)
        {
            return ServiceResult<int>.Failure(Loc.T("The applicant user does not exist or is not active."));
        }

        var aprobadoresSeleccionadosPorRol = ObtenerAprobadoresSeleccionadosPorRol(model);

        var validacionAprobadores = await ValidarAprobadoresSeleccionadosAsync(
            context,
            aprobadoresSeleccionadosPorRol,
            cancellationToken);

        if (!validacionAprobadores.Succeeded)
        {
            return ServiceResult<int>.Failure(validacionAprobadores.Message);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        int solicitudCreadaId;

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
                RazonInformarCliente = model.RazonInformarCliente?.Trim() ?? string.Empty,

                EstadoSolicitudCodigo = QcDesvCodigos.EstadoSolicitud.Borrador,
                NumeroExtensionActual = 0,

                CreadoPorUsuarioId = usuarioActualId,
                FechaCreacion = DateTime.Now
            };

            context.QcDesvSolicitudes.Add(solicitud);
            await context.SaveChangesAsync(cancellationToken);

            AgregarCsrsSolicitud(context, solicitud.Id, csrs);

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
                aprobadoresSeleccionadosPorRol: aprobadoresSeleccionadosPorRol,
                cancellationToken: cancellationToken);

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

            solicitudCreadaId = solicitud.Id;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            var detalle = ex.InnerException?.Message ?? ex.Message;

            return ServiceResult<int>.Failure(Loc.T("Error creating the request: {0}", detalle));
        }

        // La solicitud ya quedó guardada (transacción confirmada arriba): un
        // fallo al notificar por correo no debe reportarse como error de
        // creación ni, sobre todo, intentar un rollback de una transacción
        // que ya hizo commit (eso lanzaría una segunda excepción sin
        // capturar y tumbaría el circuito completo de Blazor Server).
        var primeraAprobacion = await context.QcDesvAprobaciones
            .AsNoTracking()
            .Where(x =>
                x.SolicitudId == solicitudCreadaId &&
                x.ExtensionId == null &&
                x.EsPasoActual)
            .FirstOrDefaultAsync(cancellationToken);

        if (primeraAprobacion is not null)
        {
            await _emailService.NotificarPasoEmitidoAsync(primeraAprobacion.Id, cancellationToken);
        }

        return ServiceResult<int>.Success(solicitudCreadaId, Loc.T("Request created and submitted successfully."));
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

    public async Task<List<QcDesvSolicitudConExtensiones>> ObtenerResumenConExtensionesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var solicitudes = await context.QcDesvSolicitudesResumen
            .AsNoTracking()
            .OrderByDescending(x => x.SolicitudId)
            .ToListAsync(cancellationToken);

        var solicitudIds = solicitudes.Select(x => x.SolicitudId).ToList();

        var extensiones = await context.QcDesvExtensiones
            .AsNoTracking()
            .Where(x => solicitudIds.Contains(x.SolicitudId))
            .Include(x => x.EstadoExtension)
            .Select(x => new
            {
                x.SolicitudId,
                Resumen = new QcDesvExtensionResumen
                {
                    Id = x.Id,
                    NumeroExtension = x.NumeroExtension,
                    EstadoExtensionCodigo = x.EstadoExtensionCodigo,
                    EstadoExtensionEs = x.EstadoExtension!.NombreEs,
                    EstadoExtensionEn = x.EstadoExtension!.NombreEn,
                    NuevaFechaVigencia = x.NuevaFechaVigencia
                }
            })
            .ToListAsync(cancellationToken);

        var extensionesPorSolicitud = extensiones
            .GroupBy(x => x.SolicitudId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Resumen).ToList());

        return solicitudes
            .Select(x => new QcDesvSolicitudConExtensiones
            {
                Solicitud = x,
                Extensiones = extensionesPorSolicitud.TryGetValue(x.SolicitudId, out var ext)
                    ? ext
                    : new List<QcDesvExtensionResumen>()
            })
            .ToList();
    }

    public async Task<QcDesvEstadisticas> ObtenerEstadisticasAsync(
        CancellationToken cancellationToken = default)
    {
        var solicitudesConExtensiones = await ObtenerResumenConExtensionesAsync(cancellationToken);
        var solicitudes = solicitudesConExtensiones.Select(x => x.Solicitud).ToList();
        var extensiones = solicitudesConExtensiones.SelectMany(x => x.Extensiones).ToList();

        var hoy = DateTime.Today;

        // Mismos criterios de vigencia que Desviaciones.razor: solo tiene sentido
        // hablar de vencida/por vencer/vigente una vez que la solicitud ya fue
        // aprobada -- mientras está en revisión su fecha de vigencia no aplica.
        bool EsCerrada(QcDesvSolicitudResumen s) => s.EstadoSolicitudCodigo == QcDesvCodigos.EstadoSolicitud.Cerrada;

        bool EsAprobada(QcDesvSolicitudResumen s) =>
            s.EstadoSolicitudCodigo == QcDesvCodigos.EstadoSolicitud.Aprobada ||
            s.EstadoSolicitudCodigo == QcDesvCodigos.EstadoSolicitud.ExtensionAprobada ||
            EsCerrada(s);

        bool EsRechazada(QcDesvSolicitudResumen s) => s.EstadoSolicitudCodigo == QcDesvCodigos.EstadoSolicitud.Rechazada;
        bool EsCancelada(QcDesvSolicitudResumen s) => s.EstadoSolicitudCodigo == QcDesvCodigos.EstadoSolicitud.Cancelada;

        const int diasPorVencer = 15;

        bool EsVencida(QcDesvSolicitudResumen s) =>
            EsAprobada(s) && !EsCerrada(s) && s.FechaVigencia.Date < hoy;

        bool EsPorVencer(QcDesvSolicitudResumen s) =>
            EsAprobada(s) && !EsCerrada(s) &&
            (s.FechaVigencia.Date - hoy).TotalDays is >= 0 and <= diasPorVencer;

        bool EsVigente(QcDesvSolicitudResumen s) =>
            EsAprobada(s) && !EsCerrada(s) && !EsVencida(s) && !EsPorVencer(s);

        var resueltas = solicitudes.Count(s => EsAprobada(s) || EsRechazada(s) || EsCancelada(s));
        var aprobadas = solicitudes.Count(EsAprobada);
        var rechazadas = solicitudes.Count(EsRechazada);

        var estadisticas = new QcDesvEstadisticas
        {
            TotalSolicitudes = solicitudes.Count,
            TotalExtensiones = extensiones.Count,
            Cerradas = solicitudes.Count(EsCerrada),
            Vigentes = solicitudes.Count(EsVigente),
            PorVencer = solicitudes.Count(EsPorVencer),
            Vencidas = solicitudes.Count(EsVencida),
            PorcentajeAprobacion = resueltas == 0 ? 0 : Math.Round(aprobadas * 100.0 / resueltas, 1),
            PorcentajeRechazo = resueltas == 0 ? 0 : Math.Round(rechazadas * 100.0 / resueltas, 1),
        };

        estadisticas.PorEstado = AgruparPorTop(solicitudes, s => CatalogoTexto.Nombre(s.EstadoSolicitudEs, s.EstadoSolicitudEn), incluirOtros: false);
        estadisticas.Por6M = AgruparPorTop(solicitudes, s => CatalogoTexto.Nombre(s.RequisitoProcesoEs, s.RequisitoProcesoEn), incluirOtros: false);
        estadisticas.PorTipo = AgruparPorTop(solicitudes, s => CatalogoTexto.Nombre(s.TipoDesviacionEs, s.TipoDesviacionEn), incluirOtros: false);
        estadisticas.PorEstadoExtension = AgruparPorTop(extensiones, e => CatalogoTexto.Nombre(e.EstadoExtensionEs, e.EstadoExtensionEn), incluirOtros: false);
        estadisticas.TopUnidadesNegocio = AgruparPorTop(solicitudes, s => s.UnidadNegocioAfectada, top: 8);
        estadisticas.TopSolicitantes = AgruparPorTop(solicitudes, s => s.NombreSolicitante, top: 8);
        estadisticas.TopLineas = AgruparPorTop(solicitudes, s => s.CodigoLinea, top: 8);

        estadisticas.TendenciaMensual = new List<QcDesvEtiquetaValor>();
        for (var i = 11; i >= 0; i--)
        {
            var mes = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-i);
            var conteo = solicitudes.Count(s =>
                s.FechaSolicitud.Year == mes.Year && s.FechaSolicitud.Month == mes.Month);

            estadisticas.TendenciaMensual.Add(new QcDesvEtiquetaValor
            {
                Etiqueta = mes.ToString("MMM yyyy", new System.Globalization.CultureInfo("es-MX")),
                Valor = conteo
            });
        }

        return estadisticas;
    }

    private static List<QcDesvEtiquetaValor> AgruparPorTop<T>(
        IEnumerable<T> items,
        Func<T, string?> selector,
        int? top = null,
        bool incluirOtros = true)
    {
        var grupos = items
            .Select(selector)
            .Select(v => string.IsNullOrWhiteSpace(v) ? Loc.T("No data") : v!.Trim())
            .GroupBy(v => v)
            .Select(g => new QcDesvEtiquetaValor { Etiqueta = g.Key, Valor = g.Count() })
            .OrderByDescending(x => x.Valor)
            .ToList();

        if (top is null || grupos.Count <= top)
        {
            return grupos;
        }

        var principales = grupos.Take(top.Value).ToList();

        if (incluirOtros)
        {
            var otros = grupos.Skip(top.Value).Sum(x => x.Valor);
            if (otros > 0)
            {
                principales.Add(new QcDesvEtiquetaValor { Etiqueta = Loc.T("Others"), Valor = otros });
            }
        }

        return principales;
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
            .Include(x => x.Csrs)
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
            .Include(x => x.Csrs)
            .Include(x => x.Descripcion)
            .Include(x => x.AnalisisRiesgo)
            .Include(x => x.Acciones)
            .FirstOrDefaultAsync(x => x.Id == solicitudId, cancellationToken);

        if (solicitud is null)
        {
            return ServiceResult<QcDesvSolicitudFormModel>.Failure(Loc.T("The request does not exist."));
        }

        if (solicitud.UsuarioSolicitanteId != usuarioActualId)
        {
            return ServiceResult<QcDesvSolicitudFormModel>.Failure(Loc.T("Only the original applicant can edit this request."));
        }

        if (!PuedeEditarSolicitud(solicitud.EstadoSolicitudCodigo))
        {
            return ServiceResult<QcDesvSolicitudFormModel>.Failure(Loc.T("Only draft or rejected requests can be edited."));
        }

        var accion = solicitud.Acciones
            .OrderBy(x => x.Id)
            .FirstOrDefault();

        var csrs = ObtenerCsrsDesdeSolicitud(solicitud);

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
            Csrs = csrs.Count == 0 ? new List<string> { string.Empty } : csrs,

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
            return ServiceResult<int>.Failure(Loc.T("The request is required."));
        }

        var csrs = ObtenerCsrsNormalizados(model);
        SincronizarCsrsCompatibilidad(model, csrs);

        var errores = ValidarSolicitud(model);

        if (errores.Count > 0)
        {
            return ServiceResult<int>.Failure(string.Join(" ", errores));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var solicitud = await context.QcDesvSolicitudes
            .Include(x => x.Csrs)
            .Include(x => x.Descripcion)
            .Include(x => x.AnalisisRiesgo)
            .Include(x => x.Acciones)
                .ThenInclude(x => x.Archivos)
            .FirstOrDefaultAsync(x => x.Id == model.SolicitudId, cancellationToken);

        if (solicitud is null)
        {
            return ServiceResult<int>.Failure(Loc.T("The request does not exist."));
        }

        if (solicitud.UsuarioSolicitanteId != usuarioActualId)
        {
            return ServiceResult<int>.Failure(Loc.T("Only the original applicant can edit this request."));
        }

        if (!PuedeEditarSolicitud(solicitud.EstadoSolicitudCodigo))
        {
            return ServiceResult<int>.Failure(Loc.T("Only draft or rejected requests can be edited and resubmitted."));
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
            solicitud.RazonInformarCliente = model.RazonInformarCliente?.Trim() ?? string.Empty;

            solicitud.ModificadoPorUsuarioId = usuarioActualId;
            solicitud.FechaModificacion = DateTime.Now;

            context.QcDesvSolicitudCsrs.RemoveRange(solicitud.Csrs);

            AgregarCsrsSolicitud(context, solicitud.Id, csrs);

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

            if (!string.IsNullOrWhiteSpace(model.DescripcionAccion))
            {
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
            }
            else if (accion is not null && !accion.Archivos.Any())
            {
                context.QcDesvAcciones.Remove(accion);
            }

            var aprobacionesAnteriores = await context.QcDesvAprobaciones
                .Where(x => x.SolicitudId == solicitud.Id && x.ExtensionId == null)
                .ToListAsync(cancellationToken);

            // Editar.razor no permite volver a elegir aprobadores: se conservan
            // los mismos usuarios que ya estaban asignados por rol antes del
            // rechazo, para no perder la asignación original al regenerar el
            // flujo (si no se pasan, cada paso queda sin aprobador específico
            // y las notificaciones se envían a todos los usuarios del rol).
            var aprobadoresSeleccionadosPorRol = aprobacionesAnteriores
                .Where(x => x.UsuarioAprobadorId.HasValue && x.UsuarioAprobadorId.Value > 0)
                .GroupBy(x => x.RolAprobadorCodigo)
                .ToDictionary(
                    g => g.Key,
                    g => (int?)g.OrderByDescending(x => x.FechaAsignacion).First().UsuarioAprobadorId!.Value);

            context.QcDesvAprobaciones.RemoveRange(aprobacionesAnteriores);

            await context.SaveChangesAsync(cancellationToken);

            var workflowResult = await _workflowService.CrearAprobacionesAsync(
                context,
                solicitud.Id,
                extensionId: null,
                tipoFlujoCodigo: QcDesvCodigos.TipoFlujo.Solicitud,
                aprobadoresSeleccionadosPorRol: aprobadoresSeleccionadosPorRol,
                cancellationToken: cancellationToken);

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

            return ServiceResult<int>.Success(solicitud.Id, Loc.T("Request updated and resubmitted successfully."));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            var detalle = ex.InnerException?.Message ?? ex.Message;

            return ServiceResult<int>.Failure(Loc.T("Error updating the request: {0}", detalle));
        }
    }

    private static bool PuedeEditarSolicitud(string estadoSolicitudCodigo)
    {
        return estadoSolicitudCodigo == QcDesvCodigos.EstadoSolicitud.Borrador ||
               estadoSolicitudCodigo == QcDesvCodigos.EstadoSolicitud.Rechazada;
    }

    private static List<string> ValidarSolicitud(QcDesvSolicitudFormModel model)
    {
        var errores = new List<string>();

        if (model.UsuarioSolicitanteId <= 0)
            errores.Add(Loc.T("The applicant user is required."));

        if (string.IsNullOrWhiteSpace(model.NombreSolicitante))
            errores.Add(Loc.T("The applicant name is required."));

        if (string.IsNullOrWhiteSpace(model.DepartamentoSolicitante))
            errores.Add(Loc.T("The requesting department is required."));

        if (string.IsNullOrWhiteSpace(model.UnidadNegocioAfectada))
            errores.Add(Loc.T("The affected business unit is required."));

        if (string.IsNullOrWhiteSpace(model.CodigoLinea))
            errores.Add(Loc.T("The line code is required."));

        if (string.IsNullOrWhiteSpace(model.Clientes))
            errores.Add(Loc.T("The customer is required."));

        if (model.TipoDesviacionCodigo != QcDesvCodigos.TipoDesviacion.Critica &&
            model.TipoDesviacionCodigo != QcDesvCodigos.TipoDesviacion.Estandar)
        {
            errores.Add(Loc.T("The deviation type is not valid."));
        }

        if (model.FechaVigencia.Date < model.FechaSolicitud.Date)
            errores.Add(Loc.T("The expiration date cannot be earlier than the request date."));

        if (model.CantidadPiezas.HasValue && model.CantidadPiezas.Value <= 0)
            errores.Add(Loc.T("The quantity of parts must be greater than 0."));

        if (string.IsNullOrWhiteSpace(model.NumeroParteAfectado))
            errores.Add(Loc.T("The affected part number is required."));

        if (string.IsNullOrWhiteSpace(model.RequisitoProcesoCodigo))
            errores.Add(Loc.T("The 6M category is required."));

        if (model.RequisitoProcesoCodigo == QcDesvCodigos.RequisitoProceso.Otro &&
            string.IsNullOrWhiteSpace(model.RequisitoProcesoOtro))
        {
            errores.Add(Loc.T("You must specify the 6M category when you select Other."));
        }

        var csrs = ObtenerCsrsNormalizados(model);

        if (csrs.Count == 0)
            errores.Add(Loc.T("You must enter at least one CSR."));

        if (csrs.Any(x => x.Length > 250))
            errores.Add(Loc.T("Each CSR must have a maximum of 250 characters."));

        if (model.InformarCliente && string.IsNullOrWhiteSpace(model.RazonInformarCliente))
            errores.Add(Loc.T("You must enter the reason to inform the customer."));

        if (string.IsNullOrWhiteSpace(model.ComoDebeRealizarseProceso))
            errores.Add(Loc.T("You must enter how the process should be performed."));

        if (string.IsNullOrWhiteSpace(model.ControlesOperacionActual))
            errores.Add(Loc.T("You must enter the controls of the current operation."));

        if (string.IsNullOrWhiteSpace(model.ProcesoDespuesCambioTemporal))
            errores.Add(Loc.T("You must enter how the process will run with the temporary change."));

        if (string.IsNullOrWhiteSpace(model.ControlesDuranteCambio))
            errores.Add(Loc.T("You must enter the controls during the operation with the change."));

        if (model.TipoDesviacionCodigo == QcDesvCodigos.TipoDesviacion.Critica)
        {
            if (ValorAmefInvalido(model.SeveridadActual))
                errores.Add(Loc.T("Severity must be between 1 and 10."));

            if (ValorAmefInvalido(model.OcurrenciaActual))
                errores.Add(Loc.T("Current occurrence must be between 1 and 10."));

            if (ValorAmefInvalido(model.DeteccionActual))
                errores.Add(Loc.T("Current detection must be between 1 and 10."));

            if (ValorAmefInvalido(model.OcurrenciaConCambio))
                errores.Add(Loc.T("Occurrence with change must be between 1 and 10."));

            if (ValorAmefInvalido(model.DeteccionConCambio))
                errores.Add(Loc.T("Detection with change must be between 1 and 10."));
        }

        return errores;
    }

    private static bool ValorAmefInvalido(int? valor)
    {
        return !valor.HasValue || valor.Value < 1 || valor.Value > 10;
    }

    private static List<string> ObtenerCsrsNormalizados(QcDesvSolicitudFormModel model)
    {
        var csrs = model.Csrs
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();

        if (csrs.Count == 0)
        {
            csrs = new[] { model.CSR1, model.CSR2, model.CSR3 }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .ToList();
        }

        return csrs;
    }

    private static List<string> ObtenerCsrsDesdeSolicitud(QcDesvSolicitud solicitud)
    {
        var csrs = solicitud.Csrs
            .OrderBy(x => x.Numero)
            .Select(x => x.ValorCsr)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (csrs.Count == 0)
        {
            csrs = new[] { solicitud.CSR1, solicitud.CSR2, solicitud.CSR3 }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .ToList();
        }

        return csrs;
    }

    private static void SincronizarCsrsCompatibilidad(
        QcDesvSolicitudFormModel model,
        List<string> csrs)
    {
        model.CSR1 = ObtenerCsrCompatibilidad(csrs, 0);
        model.CSR2 = ObtenerCsrCompatibilidad(csrs, 1);
        model.CSR3 = ObtenerCsrCompatibilidad(csrs, 2);
    }

    private static string? ObtenerCsrCompatibilidad(List<string> csrs, int index)
    {
        if (index < 0 || index >= csrs.Count)
        {
            return null;
        }

        var valor = csrs[index];

        return valor.Length <= 50
            ? valor
            : valor[..50];
    }

    private static Dictionary<string, int?> ObtenerAprobadoresSeleccionadosPorRol(
        QcDesvSolicitudFormModel model)
    {
        return model.AprobadoresSeleccionados
            .Where(x => !string.IsNullOrWhiteSpace(x.RolCodigo))
            .GroupBy(x => x.RolCodigo.Trim().ToUpperInvariant())
            .ToDictionary(
                x => x.Key,
                x => x.First().UsuarioAprobadorId,
                StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<ServiceResult> ValidarAprobadoresSeleccionadosAsync(
        ApplicationDbContext context,
        Dictionary<string, int?> aprobadoresSeleccionadosPorRol,
        CancellationToken cancellationToken)
    {
        foreach (var rolCodigo in RolesAprobacionSolicitud)
        {
            if (!aprobadoresSeleccionadosPorRol.TryGetValue(rolCodigo, out var usuarioId) ||
                !usuarioId.HasValue ||
                usuarioId.Value <= 0)
            {
                return ServiceResult.Failure(
                    Loc.T("You must select an approver for role {0}.", rolCodigo));
            }
        }

        var usuariosIds = aprobadoresSeleccionadosPorRol
            .Values
            .Where(x => x.HasValue && x.Value > 0)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var usuariosActivos = await context.QcDesvUsuarios
            .AsNoTracking()
            .Where(x => usuariosIds.Contains(x.Id) && x.Activo)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var usuarioId in usuariosIds)
        {
            if (!usuariosActivos.Contains(usuarioId))
            {
                return ServiceResult.Failure(
                    Loc.T("One of the selected approvers does not exist or is inactive."));
            }
        }

        var asignaciones = await context.QcDesvUsuarioRoles
            .AsNoTracking()
            .Where(x =>
                usuariosIds.Contains(x.UsuarioId) &&
                RolesAprobacionSolicitud.Contains(x.RolCodigo))
            .ToListAsync(cancellationToken);

        foreach (var rolCodigo in RolesAprobacionSolicitud)
        {
            var usuarioId = aprobadoresSeleccionadosPorRol[rolCodigo]!.Value;

            var usuarioTieneRol = asignaciones.Any(x =>
                x.UsuarioId == usuarioId &&
                x.RolCodigo == rolCodigo);

            if (!usuarioTieneRol)
            {
                return ServiceResult.Failure(
                    Loc.T("The user selected for {0} does not have that role assigned.", rolCodigo));
            }
        }

        return ServiceResult.Success();
    }

    private static void AgregarCsrsSolicitud(
        ApplicationDbContext context,
        int solicitudId,
        List<string> csrs)
    {
        for (var i = 0; i < csrs.Count; i++)
        {
            context.QcDesvSolicitudCsrs.Add(new QcDesvSolicitudCsr
            {
                SolicitudId = solicitudId,
                Numero = i + 1,
                ValorCsr = csrs[i],
                FechaCreacion = DateTime.Now
            });
        }
    }
}