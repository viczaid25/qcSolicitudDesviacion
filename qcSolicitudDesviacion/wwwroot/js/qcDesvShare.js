window.qcDesvShare = {
    downloadCardAsPng: async function (elementId, fileName) {
        const blob = await renderElementToBlob(elementId);

        if (!blob) {
            return;
        }

        downloadBlob(blob, fileName || "desviacion.png");
    },

    shareCardAsImage: async function (elementId, fileName, title, text) {
        const blob = await renderElementToBlob(elementId);

        if (!blob) {
            return;
        }

        const safeFileName = fileName || "desviacion.png";
        const file = new File([blob], safeFileName, { type: "image/png" });

        if (navigator.canShare && navigator.canShare({ files: [file] })) {
            await navigator.share({
                title: title || "Desviación aprobada",
                text: text || "Ficha de desviación aprobada",
                files: [file]
            });

            return;
        }

        downloadBlob(blob, safeFileName);
        alert("Tu navegador no permite compartir archivos directamente. Se descargó la imagen para que puedas compartirla manualmente.");
    },

    copyCurrentUrl: async function () {
        const url = window.location.href;

        if (navigator.clipboard && navigator.clipboard.writeText) {
            await navigator.clipboard.writeText(url);
            return;
        }

        const input = document.createElement("input");
        input.value = url;
        document.body.appendChild(input);
        input.select();
        document.execCommand("copy");
        document.body.removeChild(input);
    },

    printPage: function () {
        window.print();
    }
};

async function renderElementToBlob(elementId) {
    const element = document.getElementById(elementId);

    if (!element) {
        alert("No se encontró la ficha para generar la imagen.");
        return null;
    }

    if (!window.html2canvas) {
        alert("No se encontró html2canvas. Agrega html2canvas.min.js en wwwroot/lib/html2canvas o habilita el CDN.");
        return null;
    }

    const canvas = await window.html2canvas(element, {
        scale: 2,
        backgroundColor: "#ffffff",
        useCORS: true,
        logging: false
    });

    return await new Promise(resolve => {
        canvas.toBlob(blob => resolve(blob), "image/png", 1);
    });
}

function downloadBlob(blob, fileName) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");

    link.href = url;
    link.download = sanitizeFileName(fileName);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    URL.revokeObjectURL(url);
}

function sanitizeFileName(fileName) {
    return fileName.replace(/[\\/:*?"<>|]+/g, "-");
}