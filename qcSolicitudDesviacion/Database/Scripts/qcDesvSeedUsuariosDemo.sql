USE qcSolicitudDesviacion;
GO

SET IDENTITY_INSERT qcDesvUsuario ON;

IF NOT EXISTS (SELECT 1 FROM qcDesvUsuario WHERE Id = 1)
BEGIN
    INSERT INTO qcDesvUsuario
    (Id, UserName, NombreCompleto, Email, Departamento, UnidadNegocio, Activo)
    VALUES
    (1, 'demo.solicitante', 'Usuario Demo Solicitante', 'demo.solicitante@empresa.com', 'Calidad', 'Planta 1', 1);
END

IF NOT EXISTS (SELECT 1 FROM qcDesvUsuario WHERE Id = 2)
BEGIN
    INSERT INTO qcDesvUsuario
    (Id, UserName, NombreCompleto, Email, Departamento, UnidadNegocio, Activo)
    VALUES
    (2, 'demo.qms', 'Usuario Demo QMS', 'demo.qms@empresa.com', 'QMS', 'Planta 1', 1);
END

IF NOT EXISTS (SELECT 1 FROM qcDesvUsuario WHERE Id = 3)
BEGIN
    INSERT INTO qcDesvUsuario
    (Id, UserName, NombreCompleto, Email, Departamento, UnidadNegocio, Activo)
    VALUES
    (3, 'demo.apqp', 'Usuario Demo APQP', 'demo.apqp@empresa.com', 'APQP', 'Planta 1', 1);
END

SET IDENTITY_INSERT qcDesvUsuario OFF;
GO

-- Para pruebas, el usuario 1 tendrá todos los roles.
-- Después esto se reemplazará con autenticación real.
INSERT INTO qcDesvUsuarioRol (UsuarioId, RolCodigo)
SELECT 1, Codigo
FROM qcDesvRol r
WHERE NOT EXISTS (
    SELECT 1
    FROM qcDesvUsuarioRol ur
    WHERE ur.UsuarioId = 1
      AND ur.RolCodigo = r.Codigo
);
GO