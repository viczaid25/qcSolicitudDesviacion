using System.Globalization;

namespace qcSolicitudDesviacion.Localization;

/// <summary>
/// Nombre de un catálogo de la BD que ya trae NombreEs y NombreEn: en español
/// se usa NombreEs; en otro idioma, NombreEn pasado por el diccionario (así el
/// japonés sale de ja.json y, si no está, se queda en inglés).
/// </summary>
public static class CatalogoTexto
{
    public static string Nombre(string? nombreEs, string? nombreEn)
    {
        var es = nombreEs ?? string.Empty;
        var en = string.IsNullOrWhiteSpace(nombreEn) ? es : nombreEn;

        if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es")
        {
            return string.IsNullOrWhiteSpace(es) ? en : es;
        }

        return string.IsNullOrWhiteSpace(en) ? string.Empty : Loc.T(en);
    }

    /// <summary>
    /// NombreEn de los catálogos (qcDesvCat*, qcDesvRol). No se llama: solo los
    /// declara como llaves en uso para que i18n_check no los borre con --prune.
    /// Si se agrega un registro al catálogo, agrégalo aquí y en Resources/*.json.
    /// </summary>
    internal static string[] NombresCatalogo() =>
    [
        Loc.T("Critical"), Loc.T("Standard"),
        Loc.T("Manpower"), Loc.T("Machine"), Loc.T("Material"), Loc.T("Measurement"), Loc.T("Method"), Loc.T("Other"),
        Loc.T("Approved"), Loc.T("Draft"), Loc.T("Cancelled"), Loc.T("Closed"), Loc.T("Rejected"), Loc.T("Pending"), Loc.T("Skipped"),
        Loc.T("Under QC Advisor review"), Loc.T("Under APQP Coordinator review"), Loc.T("Under QC Coordinator review"),
        Loc.T("Under DGM Production / Engineering review"), Loc.T("Under DGM QC review"),
        Loc.T("Under Production / Engineering Manager review"), Loc.T("Under QC Manager review"), Loc.T("Under QMS review"),
        Loc.T("Extension approved"), Loc.T("Extension requested"), Loc.T("Extension"), Loc.T("Initial request"),
        Loc.T("Administrator"), Loc.T("QC Advisor"), Loc.T("APQP Coordinator"), Loc.T("QC Coordinator"),
        Loc.T("DGM Production / Engineering"), Loc.T("Production / Engineering Manager"), Loc.T("QC Manager"), Loc.T("Requester"),
    ];
}
