window.hirayaGoBack = (fallback) => {
    if (window.history.length > 1) {
        window.history.back();
        return;
    }
    if (fallback) {
        window.location.assign(fallback);
    }
};

window.hirayaDownload = (fileName, base64, contentType) => {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    const blob = new Blob([bytes], { type: contentType || "application/octet-stream" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName || "module";
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 2000);
};
