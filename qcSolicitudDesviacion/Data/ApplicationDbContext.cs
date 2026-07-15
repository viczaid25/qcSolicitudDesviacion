using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Models;

namespace qcSolicitudDesviacion.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<QcDesvCatTipoDesviacion> QcDesvCatTipoDesviaciones => Set<QcDesvCatTipoDesviacion>();
    public DbSet<QcDesvCatRequisitoProceso> QcDesvCatRequisitosProceso => Set<QcDesvCatRequisitoProceso>();
    public DbSet<QcDesvCatEstadoSolicitud> QcDesvCatEstadosSolicitud => Set<QcDesvCatEstadoSolicitud>();
    public DbSet<QcDesvCatEstadoAprobacion> QcDesvCatEstadosAprobacion => Set<QcDesvCatEstadoAprobacion>();
    public DbSet<QcDesvCatEstadoExtension> QcDesvCatEstadosExtension => Set<QcDesvCatEstadoExtension>();
    public DbSet<QcDesvCatTipoFlujo> QcDesvCatTiposFlujo => Set<QcDesvCatTipoFlujo>();

    public DbSet<QcDesvRol> QcDesvRoles => Set<QcDesvRol>();
    public DbSet<QcDesvUsuario> QcDesvUsuarios => Set<QcDesvUsuario>();

    public DbSet<QcDesvFlujoAprobacion> QcDesvFlujosAprobacion => Set<QcDesvFlujoAprobacion>();

    public DbSet<QcDesvSolicitud> QcDesvSolicitudes => Set<QcDesvSolicitud>();
    public DbSet<QcDesvDescripcion> QcDesvDescripciones => Set<QcDesvDescripcion>();
    public DbSet<QcDesvAnalisisRiesgo> QcDesvAnalisisRiesgos => Set<QcDesvAnalisisRiesgo>();
    public DbSet<QcDesvAccion> QcDesvAcciones => Set<QcDesvAccion>();
    public DbSet<QcDesvArchivo> QcDesvArchivos => Set<QcDesvArchivo>();
    public DbSet<QcDesvControl> QcDesvControles => Set<QcDesvControl>();

    public DbSet<QcDesvExtension> QcDesvExtensiones => Set<QcDesvExtension>();
    public DbSet<QcDesvAprobacion> QcDesvAprobaciones => Set<QcDesvAprobacion>();
    public DbSet<QcDesvHistorial> QcDesvHistoriales => Set<QcDesvHistorial>();
    public DbSet<QcDesvSolicitudCsr> QcDesvSolicitudCsrs => Set<QcDesvSolicitudCsr>();

    public DbSet<QcDesvSolicitudResumen> QcDesvSolicitudesResumen => Set<QcDesvSolicitudResumen>();

    public DbSet<QcDesvUsuarioRol> QcDesvUsuarioRoles => Set<QcDesvUsuarioRol>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigurarCatalogos(modelBuilder);
        ConfigurarSeguridad(modelBuilder);
        ConfigurarFlujoAprobacion(modelBuilder);
        ConfigurarSolicitud(modelBuilder);
        ConfigurarDetalleSolicitud(modelBuilder);
        ConfigurarExtensiones(modelBuilder);
        ConfigurarAprobaciones(modelBuilder);
        ConfigurarHistorial(modelBuilder);
        ConfigurarVistas(modelBuilder);
        ConfigurarCsrs(modelBuilder);
    }

    private static void ConfigurarCatalogos(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QcDesvCatTipoDesviacion>(entity =>
        {
            entity.ToTable("qcDesvCatTipoDesviacion");
            entity.HasKey(e => e.Codigo);

            entity.Property(e => e.Codigo).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.NombreEs).HasMaxLength(100).IsRequired();
            entity.Property(e => e.NombreEn).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Activo).HasDefaultValue(true);
        });

        modelBuilder.Entity<QcDesvCatRequisitoProceso>(entity =>
        {
            entity.ToTable("qcDesvCatRequisitoProceso");
            entity.HasKey(e => e.Codigo);

            entity.Property(e => e.Codigo).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.NombreEs).HasMaxLength(100).IsRequired();
            entity.Property(e => e.NombreEn).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Activo).HasDefaultValue(true);
        });

        modelBuilder.Entity<QcDesvCatEstadoSolicitud>(entity =>
        {
            entity.ToTable("qcDesvCatEstadoSolicitud");
            entity.HasKey(e => e.Codigo);

            entity.Property(e => e.Codigo).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.NombreEs).HasMaxLength(150).IsRequired();
            entity.Property(e => e.NombreEn).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Activo).HasDefaultValue(true);
        });

        modelBuilder.Entity<QcDesvCatEstadoAprobacion>(entity =>
        {
            entity.ToTable("qcDesvCatEstadoAprobacion");
            entity.HasKey(e => e.Codigo);

            entity.Property(e => e.Codigo).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.NombreEs).HasMaxLength(100).IsRequired();
            entity.Property(e => e.NombreEn).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Activo).HasDefaultValue(true);
        });

        modelBuilder.Entity<QcDesvCatEstadoExtension>(entity =>
        {
            entity.ToTable("qcDesvCatEstadoExtension");
            entity.HasKey(e => e.Codigo);

            entity.Property(e => e.Codigo).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.NombreEs).HasMaxLength(100).IsRequired();
            entity.Property(e => e.NombreEn).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Activo).HasDefaultValue(true);
        });

        modelBuilder.Entity<QcDesvCatTipoFlujo>(entity =>
        {
            entity.ToTable("qcDesvCatTipoFlujo");
            entity.HasKey(e => e.Codigo);

            entity.Property(e => e.Codigo).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.NombreEs).HasMaxLength(100).IsRequired();
            entity.Property(e => e.NombreEn).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Activo).HasDefaultValue(true);
        });
    }

    private static void ConfigurarSeguridad(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QcDesvRol>(entity =>
        {
            entity.ToTable("qcDesvRol");
            entity.HasKey(e => e.Codigo);

            entity.Property(e => e.Codigo).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.NombreEs).HasMaxLength(150).IsRequired();
            entity.Property(e => e.NombreEn).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Activo).HasDefaultValue(true);
        });

        modelBuilder.Entity<QcDesvUsuario>(entity =>
        {
            entity.ToTable("qcDesvUsuario");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.NombreCompleto).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Departamento).HasMaxLength(150);
            entity.Property(e => e.UnidadNegocio).HasMaxLength(150);
            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("SYSDATETIME()");

            entity.HasIndex(e => e.UserName)
                .IsUnique()
                .HasDatabaseName("UQ_qcDesvUsuario_UserName");

            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("UQ_qcDesvUsuario_Email");
        });

        modelBuilder.Entity<QcDesvUsuarioRol>(entity =>
        {
            entity.ToTable("qcDesvUsuarioRol");
            entity.HasKey(e => new { e.UsuarioId, e.RolCodigo });

            entity.Property(e => e.RolCodigo).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.FechaAsignacion).HasDefaultValueSql("SYSDATETIME()");

            entity.HasOne(e => e.Usuario)
                .WithMany(e => e.UsuariosRoles)
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Rol)
                .WithMany(e => e.UsuariosRoles)
                .HasForeignKey(e => e.RolCodigo)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarFlujoAprobacion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QcDesvFlujoAprobacion>(entity =>
        {
            entity.ToTable("qcDesvFlujoAprobacion");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TipoFlujoCodigo).HasMaxLength(30).IsUnicode(false).IsRequired();
            entity.Property(e => e.RolCodigo).HasMaxLength(50).IsUnicode(false).IsRequired();
            entity.Property(e => e.EstadoSolicitudCodigo).HasMaxLength(50).IsUnicode(false).IsRequired();
            entity.Property(e => e.NombrePasoEs).HasMaxLength(150).IsRequired();
            entity.Property(e => e.NombrePasoEn).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Activo).HasDefaultValue(true);

            entity.HasIndex(e => new { e.TipoFlujoCodigo, e.Orden })
                .IsUnique()
                .HasDatabaseName("UQ_qcDesvFlujoAprobacion_Tipo_Orden");

            entity.HasOne(e => e.TipoFlujo)
                .WithMany(e => e.FlujosAprobacion)
                .HasForeignKey(e => e.TipoFlujoCodigo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Rol)
                .WithMany(e => e.FlujosAprobacion)
                .HasForeignKey(e => e.RolCodigo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.EstadoSolicitud)
                .WithMany(e => e.FlujosAprobacion)
                .HasForeignKey(e => e.EstadoSolicitudCodigo)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarSolicitud(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QcDesvSolicitud>(entity =>
        {
            entity.ToTable("qcDesvSolicitud", table =>
            {
                table.HasCheckConstraint("CK_qcDesvSolicitud_FechaVigencia", "[FechaVigencia] >= [FechaSolicitud]");
                table.HasCheckConstraint("CK_qcDesvSolicitud_CantidadPiezas", "[CantidadPiezas] IS NULL OR [CantidadPiezas] > 0");
                table.HasCheckConstraint("CK_qcDesvSolicitud_NumeroExtensionActual", "[NumeroExtensionActual] BETWEEN 0 AND 2");
                table.HasCheckConstraint(
                    "CK_qcDesvSolicitud_RequisitoOtro",
                    "([RequisitoProcesoCodigo] = 'OTRO' AND [RequisitoProcesoOtro] IS NOT NULL) OR ([RequisitoProcesoCodigo] <> 'OTRO')");
            });

            entity.HasKey(e => e.Id);

            entity.Property(e => e.NombreSolicitante).HasMaxLength(200).IsRequired();
            entity.Property(e => e.FechaSolicitud).HasColumnType("date").HasDefaultValueSql("CONVERT(date, SYSDATETIME())");

            entity.Property(e => e.DepartamentoSolicitante).HasMaxLength(150).IsRequired();
            entity.Property(e => e.UnidadNegocioAfectada).HasMaxLength(150).IsRequired();
            entity.Property(e => e.CodigoLinea).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Clientes).HasMaxLength(500).IsRequired();

            entity.Property(e => e.TipoDesviacionCodigo).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.Property(e => e.FechaVigencia).HasColumnType("date");
            entity.Property(e => e.CantidadPiezas)
                .IsRequired(false);

            entity.Property(e => e.NumeroParteAfectado).HasMaxLength(100).IsRequired();

            entity.Property(e => e.RequisitoProcesoCodigo).HasMaxLength(30).IsUnicode(false).IsRequired();
            entity.Property(e => e.RequisitoProcesoOtro).HasMaxLength(250);

            entity.Property(e => e.CSR1).HasMaxLength(50);
            entity.Property(e => e.CSR2).HasMaxLength(50);
            entity.Property(e => e.CSR3).HasMaxLength(50);

            entity.Property(e => e.RazonInformarCliente).HasMaxLength(1000).IsRequired();

            entity.Property(e => e.EstadoSolicitudCodigo)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("BORRADOR")
                .IsRequired();

            entity.Property(e => e.NumeroExtensionActual).HasDefaultValue((byte)0);
            entity.Property(e => e.NotificacionVencidaEnviada).HasDefaultValue(false);

            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => e.EstadoSolicitudCodigo).HasDatabaseName("IX_qcDesvSolicitud_Estado");
            entity.HasIndex(e => e.UsuarioSolicitanteId).HasDatabaseName("IX_qcDesvSolicitud_Solicitante");
            entity.HasIndex(e => e.FechaSolicitud).HasDatabaseName("IX_qcDesvSolicitud_FechaSolicitud");
            entity.HasIndex(e => e.TipoDesviacionCodigo).HasDatabaseName("IX_qcDesvSolicitud_TipoDesviacion");
            entity.HasIndex(e => e.NumeroParteAfectado).HasDatabaseName("IX_qcDesvSolicitud_NumeroParte");

            entity.HasOne(e => e.UsuarioSolicitante)
                .WithMany(e => e.SolicitudesComoSolicitante)
                .HasForeignKey(e => e.UsuarioSolicitanteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreadoPorUsuario)
                .WithMany(e => e.SolicitudesCreadas)
                .HasForeignKey(e => e.CreadoPorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ModificadoPorUsuario)
                .WithMany()
                .HasForeignKey(e => e.ModificadoPorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TipoDesviacion)
                .WithMany(e => e.Solicitudes)
                .HasForeignKey(e => e.TipoDesviacionCodigo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.RequisitoProceso)
                .WithMany(e => e.Solicitudes)
                .HasForeignKey(e => e.RequisitoProcesoCodigo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.EstadoSolicitud)
                .WithMany(e => e.Solicitudes)
                .HasForeignKey(e => e.EstadoSolicitudCodigo)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarDetalleSolicitud(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QcDesvDescripcion>(entity =>
        {
            entity.ToTable("qcDesvDescripcion");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ComoDebeRealizarseProceso).IsRequired();
            entity.Property(e => e.ControlesOperacionActual).IsRequired();
            entity.Property(e => e.ProcesoDespuesCambioTemporal).IsRequired();
            entity.Property(e => e.ControlesDuranteCambio).IsRequired();
            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("SYSDATETIME()");

            entity.HasIndex(e => e.SolicitudId)
                .IsUnique()
                .HasDatabaseName("UQ_qcDesvDescripcion_Solicitud");

            entity.HasOne(e => e.Solicitud)
                .WithOne(e => e.Descripcion)
                .HasForeignKey<QcDesvDescripcion>(e => e.SolicitudId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreadoPorUsuario)
                .WithMany()
                .HasForeignKey(e => e.CreadoPorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ModificadoPorUsuario)
                .WithMany()
                .HasForeignKey(e => e.ModificadoPorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QcDesvAnalisisRiesgo>(entity =>
        {
            entity.ToTable("qcDesvAnalisisRiesgo", table =>
            {
                table.HasCheckConstraint("CK_qcDesvAnalisisRiesgo_Severidad", "[SeveridadActual] BETWEEN 1 AND 10");
                table.HasCheckConstraint("CK_qcDesvAnalisisRiesgo_OcurrenciaActual", "[OcurrenciaActual] BETWEEN 1 AND 10");
                table.HasCheckConstraint("CK_qcDesvAnalisisRiesgo_DeteccionActual", "[DeteccionActual] BETWEEN 1 AND 10");
                table.HasCheckConstraint("CK_qcDesvAnalisisRiesgo_OcurrenciaConCambio", "[OcurrenciaConCambio] BETWEEN 1 AND 10");
                table.HasCheckConstraint("CK_qcDesvAnalisisRiesgo_DeteccionConCambio", "[DeteccionConCambio] BETWEEN 1 AND 10");
                table.HasTrigger("qcDesvTR_AnalisisRiesgo_SoloCritica");
            });

            entity.HasKey(e => e.Id);

            entity.Property(e => e.RpnActual)
                .HasColumnType("int")
                .HasComputedColumnSql(
                    "CONVERT(int, [SeveridadActual]) * CONVERT(int, [OcurrenciaActual]) * CONVERT(int, [DeteccionActual])",
                    stored: true);

            entity.Property(e => e.RpnConCambio)
                .HasColumnType("int")
                .HasComputedColumnSql(
                    "CONVERT(int, [SeveridadActual]) * CONVERT(int, [OcurrenciaConCambio]) * CONVERT(int, [DeteccionConCambio])",
                    stored: true);

            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("SYSDATETIME()");

            entity.HasIndex(e => e.SolicitudId)
                .IsUnique()
                .HasDatabaseName("UQ_qcDesvAnalisisRiesgo_Solicitud");

            entity.HasOne(e => e.Solicitud)
                .WithOne(e => e.AnalisisRiesgo)
                .HasForeignKey<QcDesvAnalisisRiesgo>(e => e.SolicitudId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreadoPorUsuario)
                .WithMany()
                .HasForeignKey(e => e.CreadoPorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ModificadoPorUsuario)
                .WithMany()
                .HasForeignKey(e => e.ModificadoPorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QcDesvAccion>(entity =>
        {
            entity.ToTable("qcDesvAccion");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.DescripcionAccion).IsRequired();
            entity.Property(e => e.Responsable).HasMaxLength(200);
            entity.Property(e => e.FechaCompromiso).HasColumnType("date");
            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("SYSDATETIME()");

            entity.HasOne(e => e.Solicitud)
                .WithMany(e => e.Acciones)
                .HasForeignKey(e => e.SolicitudId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreadoPorUsuario)
                .WithMany()
                .HasForeignKey(e => e.CreadoPorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ModificadoPorUsuario)
                .WithMany()
                .HasForeignKey(e => e.ModificadoPorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QcDesvArchivo>(entity =>
        {
            entity.ToTable("qcDesvArchivo", table =>
            {
                table.HasCheckConstraint("CK_qcDesvArchivo_Tamano", "[TamanoBytes] > 0");
            });

            entity.HasKey(e => e.Id);

            entity.Property(e => e.NombreOriginal).HasMaxLength(255).IsRequired();
            entity.Property(e => e.NombreGuardado).HasMaxLength(255).IsRequired();
            entity.Property(e => e.RutaRelativa).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(150);
            entity.Property(e => e.FechaSubida).HasDefaultValueSql("SYSDATETIME()");

            entity.HasIndex(e => e.SolicitudId).HasDatabaseName("IX_qcDesvArchivo_Solicitud");

            entity.HasOne(e => e.Solicitud)
                .WithMany(e => e.Archivos)
                .HasForeignKey(e => e.SolicitudId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Accion)
                .WithMany(e => e.Archivos)
                .HasForeignKey(e => e.AccionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SubidoPorUsuario)
                .WithMany()
                .HasForeignKey(e => e.SubidoPorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QcDesvControl>(entity =>
        {
            entity.ToTable("qcDesvControl");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.NumeroLote).HasMaxLength(100).IsRequired();
            entity.Property(e => e.NumeroCase).HasMaxLength(100).IsRequired();
            entity.Property(e => e.FechaCierre).HasDefaultValueSql("SYSDATETIME()");

            entity.HasIndex(e => e.SolicitudId)
                .IsUnique()
                .HasDatabaseName("UQ_qcDesvControl_Solicitud");

            entity.HasOne(e => e.Solicitud)
                .WithOne(e => e.Control)
                .HasForeignKey<QcDesvControl>(e => e.SolicitudId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CerradoPorUsuario)
                .WithMany()
                .HasForeignKey(e => e.CerradoPorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarExtensiones(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QcDesvExtension>(entity =>
        {
            entity.ToTable("qcDesvExtension", table =>
            {
                table.HasCheckConstraint("CK_qcDesvExtension_Numero", "[NumeroExtension] BETWEEN 1 AND 2");
                table.HasCheckConstraint("CK_qcDesvExtension_NuevaFecha", "[NuevaFechaVigencia] > [FechaVigenciaAnterior]");
                table.HasTrigger("qcDesvTR_Extension_ValidarLimite");
            });

            entity.HasKey(e => e.Id);

            entity.Property(e => e.FechaVigenciaAnterior).HasColumnType("date");
            entity.Property(e => e.NuevaFechaVigencia).HasColumnType("date");
            entity.Property(e => e.RazonExtension).IsRequired();

            entity.Property(e => e.EstadoExtensionCodigo)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("PENDIENTE")
                .IsRequired();

            entity.Property(e => e.FechaSolicitud).HasDefaultValueSql("SYSDATETIME()");

            entity.HasIndex(e => new { e.SolicitudId, e.NumeroExtension })
                .IsUnique()
                .HasDatabaseName("UQ_qcDesvExtension_Solicitud_Numero");

            entity.HasIndex(e => e.SolicitudId)
                .HasDatabaseName("IX_qcDesvExtension_Solicitud");

            entity.HasOne(e => e.Solicitud)
                .WithMany(e => e.Extensiones)
                .HasForeignKey(e => e.SolicitudId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.EstadoExtension)
                .WithMany(e => e.Extensiones)
                .HasForeignKey(e => e.EstadoExtensionCodigo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SolicitadaPorUsuario)
                .WithMany()
                .HasForeignKey(e => e.SolicitadaPorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarAprobaciones(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QcDesvAprobacion>(entity =>
        {
            entity.ToTable("qcDesvAprobacion", table =>
            {
                table.HasCheckConstraint(
                    "CK_qcDesvAprobacion_TipoExtension",
                    "([TipoFlujoCodigo] = 'SOLICITUD' AND [ExtensionId] IS NULL) OR ([TipoFlujoCodigo] = 'EXTENSION' AND [ExtensionId] IS NOT NULL)");
            });

            entity.HasKey(e => e.Id);

            entity.Property(e => e.TipoFlujoCodigo)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("SOLICITUD")
                .IsRequired();

            entity.Property(e => e.RolAprobadorCodigo).HasMaxLength(50).IsUnicode(false).IsRequired();

            entity.Property(e => e.EstadoAprobacionCodigo)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("PENDIENTE")
                .IsRequired();

            entity.Property(e => e.EsPasoActual).HasDefaultValue(false);
            entity.Property(e => e.FechaAsignacion).HasDefaultValueSql("SYSDATETIME()");

            entity.HasIndex(e => new { e.SolicitudId, e.Orden })
                .IsUnique()
                .HasFilter("[ExtensionId] IS NULL")
                .HasDatabaseName("UQ_qcDesvAprobacion_Normal");

            entity.HasIndex(e => new { e.SolicitudId, e.ExtensionId, e.Orden })
                .IsUnique()
                .HasFilter("[ExtensionId] IS NOT NULL")
                .HasDatabaseName("UQ_qcDesvAprobacion_Extension");

            entity.HasIndex(e => new { e.RolAprobadorCodigo, e.EstadoAprobacionCodigo, e.EsPasoActual })
                .HasDatabaseName("IX_qcDesvAprobacion_PendientesPorRol");

            entity.HasOne(e => e.Solicitud)
                .WithMany(e => e.Aprobaciones)
                .HasForeignKey(e => e.SolicitudId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Extension)
                .WithMany(e => e.Aprobaciones)
                .HasForeignKey(e => e.ExtensionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TipoFlujo)
                .WithMany(e => e.Aprobaciones)
                .HasForeignKey(e => e.TipoFlujoCodigo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.RolAprobador)
                .WithMany(e => e.Aprobaciones)
                .HasForeignKey(e => e.RolAprobadorCodigo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.UsuarioAprobador)
                .WithMany()
                .HasForeignKey(e => e.UsuarioAprobadorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.EstadoAprobacion)
                .WithMany(e => e.Aprobaciones)
                .HasForeignKey(e => e.EstadoAprobacionCodigo)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarHistorial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QcDesvHistorial>(entity =>
        {
            entity.ToTable("qcDesvHistorial");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Accion).HasMaxLength(150).IsRequired();
            entity.Property(e => e.EstadoAnteriorCodigo).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.EstadoNuevoCodigo).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.FechaMovimiento).HasDefaultValueSql("SYSDATETIME()");

            entity.HasIndex(e => e.SolicitudId)
                .HasDatabaseName("IX_qcDesvHistorial_Solicitud");

            entity.HasOne(e => e.Solicitud)
                .WithMany(e => e.Historiales)
                .HasForeignKey(e => e.SolicitudId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Extension)
                .WithMany(e => e.Historiales)
                .HasForeignKey(e => e.ExtensionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.EstadoAnterior)
                .WithMany()
                .HasForeignKey(e => e.EstadoAnteriorCodigo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.EstadoNuevo)
                .WithMany()
                .HasForeignKey(e => e.EstadoNuevoCodigo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Usuario)
                .WithMany()
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarVistas(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QcDesvSolicitudResumen>(entity =>
        {
            entity.ToView("qcDesvVW_SolicitudesResumen");
            entity.HasKey(e => e.SolicitudId);

            entity.Property(e => e.FechaSolicitud).HasColumnType("date");
            entity.Property(e => e.FechaVigencia).HasColumnType("date");

            entity.Property(e => e.NombreSolicitante).HasMaxLength(200);
            entity.Property(e => e.DepartamentoSolicitante).HasMaxLength(150);
            entity.Property(e => e.UnidadNegocioAfectada).HasMaxLength(150);
            entity.Property(e => e.CodigoLinea).HasMaxLength(50);
            entity.Property(e => e.Clientes).HasMaxLength(500);
            entity.Property(e => e.NumeroParteAfectado).HasMaxLength(100);

            entity.Property(e => e.TipoDesviacionCodigo).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.TipoDesviacionEs).HasMaxLength(100);
            entity.Property(e => e.TipoDesviacionEn).HasMaxLength(100);

            entity.Property(e => e.RequisitoProcesoCodigo).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.RequisitoProcesoEs).HasMaxLength(100);
            entity.Property(e => e.RequisitoProcesoEn).HasMaxLength(100);

            entity.Property(e => e.EstadoSolicitudCodigo).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.EstadoSolicitudEs).HasMaxLength(150);
            entity.Property(e => e.EstadoSolicitudEn).HasMaxLength(150);
        });
    }

    private static void ConfigurarCsrs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QcDesvSolicitudCsr>(entity =>
        {
            entity.ToTable("qcDesvSolicitudCsr");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Numero)
                .IsRequired();

            entity.Property(e => e.ValorCsr)
                .HasMaxLength(250)
                .IsRequired();

            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("SYSDATETIME()");

            entity.HasIndex(e => new { e.SolicitudId, e.Numero })
                .IsUnique()
                .HasDatabaseName("UQ_qcDesvSolicitudCsr_Solicitud_Numero");

            entity.HasOne(e => e.Solicitud)
                .WithMany(e => e.Csrs)
                .HasForeignKey(e => e.SolicitudId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}