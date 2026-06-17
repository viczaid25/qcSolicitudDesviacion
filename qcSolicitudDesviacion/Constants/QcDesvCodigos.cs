namespace qcSolicitudDesviacion.Constants;

public static class QcDesvCodigos
{
    public static class TipoDesviacion
    {
        public const string Critica = "CRITICA";
        public const string Estandar = "ESTANDAR";
    }

    public static class RequisitoProceso
    {
        public const string Metodo = "METODO";
        public const string Materia = "MATERIA";
        public const string ManoObra = "MANO_OBRA";
        public const string Maquina = "MAQUINA";
        public const string Medicion = "MEDICION";
        public const string Otro = "OTRO";
    }

    public static class EstadoSolicitud
    {
        public const string Borrador = "BORRADOR";
        public const string EnRevisionQms = "EN_REVISION_QMS";
        public const string EnRevisionApqp = "EN_REVISION_APQP";
        public const string EnRevisionCoordinadorQc = "EN_REVISION_COORD_QC";
        public const string EnRevisionManagerQc = "EN_REVISION_MANAGER_QC";
        public const string EnRevisionManagerProdIng = "EN_REVISION_MANAGER_PROD_ING";
        public const string EnRevisionAdvisorQc = "EN_REVISION_ADVISOR_QC";
        public const string Aprobada = "APROBADA";
        public const string Cerrada = "CERRADA";
        public const string Rechazada = "RECHAZADA";
        public const string Cancelada = "CANCELADA";
        public const string ExtensionSolicitada = "EXTENSION_SOLICITADA";
        public const string EnRevisionDgmQc = "EN_REVISION_DGM_QC";
        public const string EnRevisionDgmProdIng = "EN_REVISION_DGM_PROD_ING";
        public const string ExtensionAprobada = "EXTENSION_APROBADA";
    }

    public static class EstadoAprobacion
    {
        public const string Pendiente = "PENDIENTE";
        public const string Aprobado = "APROBADO";
        public const string Rechazado = "RECHAZADO";
        public const string Cancelado = "CANCELADO";
        public const string Omitido = "OMITIDO";
    }

    public static class EstadoExtension
    {
        public const string Pendiente = "PENDIENTE";
        public const string Aprobada = "APROBADA";
        public const string Rechazada = "RECHAZADA";
        public const string Cancelada = "CANCELADA";
    }

    public static class TipoFlujo
    {
        public const string Solicitud = "SOLICITUD";
        public const string Extension = "EXTENSION";
    }

    public static class Rol
    {
        public const string Solicitante = "SOLICITANTE";
        public const string Qms = "QMS";
        public const string CoordApqp = "COORD_APQP";
        public const string CoordQc = "COORD_QC";
        public const string ManagerQc = "MANAGER_QC";
        public const string ManagerProdIng = "MANAGER_PROD_ING";
        public const string AdvisorQc = "ADVISOR_QC";
        public const string DgmQc = "DGM_QC";
        public const string DgmProdIng = "DGM_PROD_ING";
        public const string Admin = "ADMIN";
    }
}