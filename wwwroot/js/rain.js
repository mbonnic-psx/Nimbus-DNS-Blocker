window.nimbusRain = (function () {
    let raf = null;
    let drops = [];
    let lastW = 0, lastH = 0;

    function resize(canvas) {
        const dpr = window.devicePixelRatio || 1;
        const rect = canvas.getBoundingClientRect();
        const w = Math.floor(rect.width * dpr);
        const h = Math.floor(rect.height * dpr);
        if (w === lastW && h === lastH) return;
        lastW = w; lastH = h;
        canvas.width = w;
        canvas.height = h;
        // re-seed
        drops = [];
        const count = Math.max(60, Math.floor((w * h) / 220000));
        for (let i = 0; i < count; i++) drops.push(makeDrop(w, h));
    }

    function makeDrop(w, h) {
        return {
            x: Math.random() * w,
            y: Math.random() * h,
            len: 8 + Math.random() * 18,
            spd: 2.5 + Math.random() * 5.5,
            drift: -0.3 + Math.random() * 0.6,
            a: 0.12 + Math.random() * 0.18
        };
    }

    function tick(canvas) {
        const ctx = canvas.getContext("2d");
        resize(canvas);

        const w = canvas.width, h = canvas.height;
        ctx.clearRect(0, 0, w, h);

        // subtle rain strokes
        for (let i = 0; i < drops.length; i++) {
            const d = drops[i];
            ctx.globalAlpha = d.a;
            ctx.beginPath();
            ctx.moveTo(d.x, d.y);
            ctx.lineTo(d.x + d.drift * d.len, d.y + d.len);
            ctx.lineWidth = 1;
            ctx.strokeStyle = "#CDEBFF";
            ctx.stroke();

            d.y += d.spd;
            d.x += d.drift;

            if (d.y > h + 20 || d.x < -20 || d.x > w + 20) {
                drops[i] = makeDrop(w, h);
                drops[i].y = -30;
            }
        }

        ctx.globalAlpha = 1;
        raf = requestAnimationFrame(() => tick(canvas));
    }

    function start(canvasId) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        if (raf) cancelAnimationFrame(raf);

        // scale to layout size
        canvas.style.width = "100%";
        canvas.style.height = "100%";

        const onResize = () => resize(canvas);
        window.addEventListener("resize", onResize);

        tick(canvas);
    }

    return { start };
})();
