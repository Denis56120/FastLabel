// --- 1. CHARGEMENT DES RESSOURCES EXTERNES ---
async function loadExternalResources() {
    // Polices Google Fonts
    if (!document.getElementById('google-fonts-fastlabel')) {
        const preconnect1 = document.createElement('link');
        preconnect1.rel = 'preconnect';
        preconnect1.href = 'https://fonts.googleapis.com';
        document.head.appendChild(preconnect1);

        const preconnect2 = document.createElement('link');
        preconnect2.rel = 'preconnect';
        preconnect2.href = 'https://fonts.gstatic.com';
        preconnect2.crossOrigin = 'anonymous';
        document.head.appendChild(preconnect2);

        const fontLink = document.createElement('link');
        fontLink.id = 'google-fonts-fastlabel';
        fontLink.rel = 'stylesheet';
        fontLink.href = 'https://fonts.googleapis.com/css2?family=Inter:ital,wght@0,400;0,700;1,400;1,700&family=JetBrains+Mono:ital,wght@0,400;0,700;1,400;1,700&family=Lora:ital,wght@0,400;0,700;1,400;1,700&family=Share+Tech+Mono&display=swap';
        document.head.appendChild(fontLink);
    }

    // Scripts tiers (Fabric.js, qrcode, JsBarcode)
    const dependencies = [
        { name: 'fabric', check: () => typeof fabric !== 'undefined', src: 'https://cdnjs.cloudflare.com/ajax/libs/fabric.js/5.3.1/fabric.min.js' },
        { name: 'qrcode', check: () => typeof qrcode !== 'undefined', src: 'https://cdnjs.cloudflare.com/ajax/libs/qrcode-generator/1.4.4/qrcode.min.js' },
        { name: 'JsBarcode', check: () => typeof JsBarcode !== 'undefined', src: 'https://cdnjs.cloudflare.com/ajax/libs/jsbarcode/3.11.5/JsBarcode.all.min.js' }
    ];

    for (const dep of dependencies) {
        if (!dep.check()) {
            await new Promise((resolve, reject) => {
                const script = document.createElement('script');
                script.src = dep.src;
                script.onload = resolve;
                script.onerror = reject;
                document.head.appendChild(script);
            });
        }
    }
}

// --- 2. CONFIGURATION DES OBJETS ---
function configureObjectControls(obj) {
    if (!obj) return;
    obj.set('lockRotation', true);
    obj.setControlVisible('mtr', false);

    if (obj.isType('i-text') || obj.isType('text') || obj.isType('textbox')) {
        obj.setControlsVisibility({
            mtr: false,
            ml: true, 
            mr: true,
            mt: false,
            mb: false
        });
    }
}

// --- 3. UTILITAIRES DE TAILLE DE POLICE ---
const ALLOWED_SIZES = [12, 14, 16, 18, 24, 28, 32, 40, 48, 56, 64, 72, 96];
function getClosestAllowedSize(rawSize) {
    return ALLOWED_SIZES.reduce((prev, curr) =>
        Math.abs(curr - rawSize) < Math.abs(prev - rawSize) ? curr : prev
    );
}

// --- 4. POINT D'ENTRÉE PRINCIPAL DE L'ÉDITEUR ---
export async function initEditor(dotNetHelper, canvasId, initialJson, width, height) {
    await loadExternalResources();

    const canvas = new fabric.Canvas(canvasId, {
        width: width,
        height: height,
        backgroundColor: '#ffffff'
    });

    // Chargement initial du JSON
    if (initialJson) {
        try {
            const parsed = typeof initialJson === 'string' ? JSON.parse(initialJson) : initialJson;
            canvas.loadFromJSON(parsed, () => {
                canvas.getObjects().forEach(obj => configureObjectControls(obj));
                canvas.renderAll();
            });
        } catch (e) {
            console.error("Erreur parsing JSON", e);
        }
    }

    // --- NOTIFICATIONS ET SÉLECTION ---
    function notifySelection(obj) {
        if (!obj) {
            dotNetHelper.invokeMethodAsync('OnObjectSelected', null);
            return;
        }

        let type = obj.type;
        if (obj.isType('textbox') || obj.isType('i-text') || obj.isType('text')) {
            type = 'textbox';
        } else if (type === 'group') {
            const items = obj.getObjects();
            if (items.some(item => item.isType('textbox') || item.isType('i-text') || item.isType('text'))) {
                type = 'text-rectangle';
            }
        }
        dotNetHelper.invokeMethodAsync('OnObjectSelected', type);
    }

    const notify = () => {
        const json = JSON.stringify(canvas.toJSON());
        dotNetHelper.invokeMethodAsync('UpdateJsonData', json);
    };

    // --- MAGNÉTISME & GUIDES ---
    const snappingDistance = 5;
    let alignVerticalLine = null;
    let alignHorizontalLine = null;

    function showGuide(isVertical, pos) {
        const lineRef = isVertical ? alignVerticalLine : alignHorizontalLine;
        const coords = isVertical ? [pos, 0, pos, canvas.height] : [0, pos, canvas.width, pos];

        if (!lineRef) {
            const newLine = new fabric.Line(coords, {
                stroke: 'rgba(255, 0, 0, 0.6)',
                strokeWidth: 1,
                selectable: false,
                evented: false,
                excludeFromExport: true
            });
            canvas.add(newLine);
            if (isVertical) alignVerticalLine = newLine; else alignHorizontalLine = newLine;
        } else {
            lineRef.set(isVertical ? { x1: pos, x2: pos, y2: canvas.height } : { y1: pos, y2: pos, x2: canvas.width });
            canvas.bringToFront(lineRef);
            lineRef.set('visible', true);
        }
    }

    function hideGuides() {
        if (alignVerticalLine) alignVerticalLine.set('visible', false);
        if (alignHorizontalLine) alignHorizontalLine.set('visible', false);
    }

    // --- ÉVÉNEMENTS CANVAS ---
    canvas.on('object:modified', (e) => {
        const obj = e.target;
        if (!obj) return;

        if (obj.isType('i-text') || obj.isType('text') || obj.isType('textbox')) {
            const scale = Math.max(obj.scaleX, obj.scaleY);
            const snappedSize = getClosestAllowedSize(obj.fontSize * scale);
            obj.set({ fontSize: snappedSize, scaleX: 1, scaleY: 1 });
            obj.initDimensions();
            obj.setCoords();

            if (obj.boundRect) {
                const padding = 10;
                obj.boundRect.set({
                    left: obj.left - padding,
                    top: obj.top - padding,
                    width: (obj.width * obj.scaleX) + padding * 2,
                    height: (obj.height * obj.scaleY) + padding * 2,
                    scaleX: 1,
                    scaleY: 1
                });
                obj.boundRect.setCoords();
            }
        } else if (obj.isType('rect')) {
            // Aplatit l'échelle pour garder un contour et des coins arrondis bien réguliers
            obj.set({
                width: obj.width * obj.scaleX,
                height: obj.height * obj.scaleY,
                scaleX: 1,
                scaleY: 1
            });
            obj.setCoords();
        }
        notify();
    });

    canvas.on('object:added', (e) => { configureObjectControls(e.target); notify(); });
    canvas.on('object:removed', notify);
    canvas.on('selection:created', (e) => notifySelection(e.selected[0]));
    canvas.on('selection:updated', (e) => notifySelection(e.selected[0]));
    canvas.on('selection:cleared', () => notifySelection(null));

    canvas.on('object:moving', (e) => {
        const obj = e.target;
        if (obj === alignVerticalLine || obj === alignHorizontalLine) return;

        const objCenterX = obj.left + (obj.width * obj.scaleX) / 2;
        const objCenterY = obj.top + (obj.height * obj.scaleY) / 2;
        let snappedH = false, snappedV = false;

        hideGuides();

        if (Math.abs(objCenterX - canvas.width / 2) < snappingDistance) {
            obj.set({ left: canvas.width / 2 - (obj.width * obj.scaleX) / 2 });
            showGuide(true, canvas.width / 2);
            snappedH = true;
        }

        if (Math.abs(objCenterY - canvas.height / 2) < snappingDistance) {
            obj.set({ top: canvas.height / 2 - (obj.height * obj.scaleY) / 2 });
            showGuide(false, canvas.height / 2);
            snappedV = true;
        }

        if (obj.boundRect) {
            const padding = 10;
            obj.boundRect.set({
                left: obj.left - padding,
                top: obj.top - padding
            });
            obj.boundRect.setCoords();
        }

        obj.setCoords();
        canvas.renderAll();
    });

    canvas.on('mouse:up', () => { hideGuides(); canvas.renderAll(); });

    // --- RACCOURCIS CLAVIER ---
    let _clipboard = null;
    const handleKeyDown = (e) => {
        const activeObj = canvas.getActiveObject();
        if (['INPUT', 'TEXTAREA', 'SELECT'].includes(e.target.tagName) || (activeObj && activeObj.isEditing)) return;

        if ((e.key === 'Delete' || e.key === 'Backspace') && activeObj) {
            e.preventDefault();
            const activeObjects = canvas.getActiveObjects();
            if (activeObjects.length) {
                activeObjects.forEach(obj => { if (obj.boundRect) canvas.remove(obj.boundRect); });
                canvas.remove(...activeObjects);
                canvas.discardActiveObject();
                canvas.renderAll();
                notify();
            }
        }
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'c' && activeObj) {
            e.preventDefault();
            activeObj.clone((cloned) => { _clipboard = cloned; });
        }
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'v' && _clipboard) {
            e.preventDefault();
            _clipboard.clone((clonedObj) => {
                canvas.discardActiveObject();
                clonedObj.set({ left: clonedObj.left + 15, top: clonedObj.top + 15, evented: true });
                configureObjectControls(clonedObj);
                canvas.add(clonedObj);
                _clipboard.top += 15; _clipboard.left += 15;
                canvas.setActiveObject(clonedObj);
                canvas.requestRenderAll();
                notify();
            });
        }
    };
    window.addEventListener('keydown', handleKeyDown);

    // --- API EXPOSÉE À BLAZOR ---
    return {
        addText: (text, fontFamily = 'Inter') => {
            const textObj = new fabric.Textbox(text, {
                left: 20, top: 20, width: 180, fontSize: 18, scaleX: 1, scaleY: 1, fill: '#000000', fontFamily: fontFamily, splitByGrapheme: true
            });
            configureObjectControls(textObj);
            canvas.add(textObj);
            canvas.setActiveObject(textObj);
            notify();
        },
        addRectangle: () => {
            const rect = new fabric.Rect({
                    left: 50,
                    top: 50,
                    width: 150,
                    height: 80,
                    fill: 'transparent',
                    stroke: '#000000',
                    strokeWidth: 2,
                    scaleX: 1,
                    scaleY: 1,
                    rx: 8,
                    ry: 8
                });
            configureObjectControls(rect);
            canvas.add(rect);
            canvas.setActiveObject(rect);
            notify();
        },
        addBarcode: (barcodeData) => {
            try {
                const tempCanvas = document.createElement('canvas');
                JsBarcode(tempCanvas, barcodeData || "12345678", { format: "CODE128", lineColor: "#000", width: 2, height: 50, displayValue: true });
                fabric.Image.fromURL(tempCanvas.toDataURL("image/png"), (img) => {
                    img.set({ left: 20, top: 20, scaleX: 1, scaleY: 1 });
                    configureObjectControls(img);
                    canvas.add(img);
                    canvas.setActiveObject(img);
                    canvas.renderAll();
                    notify();
                });
            } catch (e) { console.error(e); }
        },
        addImage: (url) => {
            fabric.Image.fromURL(url, (img) => {
                img.set({ left: 50, top: 50, scaleX: 1, scaleY: 1 });
                configureObjectControls(img);
                canvas.add(img);
                canvas.setActiveObject(img);
                canvas.renderAll();
                notify();
            }, { crossOrigin: 'anonymous' });
        },
        addQrCode: (textData) => {
            try {
                const qr = qrcode(4, 'M');
                qr.addData(textData || "https://fastlabel");
                qr.make();
                const count = qr.getModuleCount(), tileSize = 6, margin = 12, size = count * tileSize + margin * 2;
                const tempCanvas = document.createElement('canvas');
                tempCanvas.width = size; tempCanvas.height = size;
                const ctx = tempCanvas.getContext('2d');
                ctx.fillStyle = '#ffffff'; ctx.fillRect(0, 0, size, size);
                ctx.fillStyle = '#000000';
                for (let r = 0; r < count; r++) {
                    for (let c = 0; c < count; c++) {
                        if (qr.isDark(r, c)) ctx.fillRect(margin + c * tileSize, margin + r * tileSize, tileSize, tileSize);
                    }
                }
                fabric.Image.fromURL(tempCanvas.toDataURL("image/png"), (img) => {
                    img.set({ left: 20, top: 20, scaleX: 1, scaleY: 1 });
                    configureObjectControls(img);
                    canvas.add(img);
                    canvas.setActiveObject(img);
                    canvas.renderAll();
                    notify();
                });
            } catch (e) { console.error(e); }
        },
        getActiveObjectProperties: () => {
            const obj = canvas.getActiveObject();
            if (!obj) return null;
            return {
                type: obj.type,
                text: obj.text || '',
                fontSize: obj.fontSize || 18,
                fontFamily: obj.fontFamily || 'Inter',
                fontWeight: obj.fontWeight === 'bold',
                fontStyle: obj.fontStyle === 'italic',
                underline: obj.underline || false,
                linethrough: obj.linethrough || false,
                rx: obj.rx || 0,
                hasBoundRect: !!obj.boundRect
            };
        },
        resizeCanvas: (w, h) => { canvas.setWidth(w); canvas.setHeight(h); canvas.renderAll(); notify(); },
        deleteSelected: () => {
            const activeObjects = canvas.getActiveObjects();
            if (activeObjects.length) {
                activeObjects.forEach(obj => { if (obj.boundRect) canvas.remove(obj.boundRect); });
                canvas.remove(...activeObjects);
                canvas.discardActiveObject();
                canvas.renderAll();
                notify();
            }
        },
        bringForward: () => { const o = canvas.getActiveObject(); if (o) { canvas.bringForward(o); canvas.renderAll(); notify(); } },
        sendBackwards: () => { const o = canvas.getActiveObject(); if (o) { canvas.sendBackwards(o); canvas.renderAll(); notify(); } },
        setFontFamily: (font) => { const o = canvas.getActiveObject(); if (o && o.set) { o.set('fontFamily', font); o.initDimensions(); o.setCoords(); canvas.renderAll(); notify(); } },
        toggleBold: () => { const o = canvas.getActiveObject(); if (o && o.set) { o.set('fontWeight', o.fontWeight === 'bold' ? 'normal' : 'bold'); o.initDimensions(); o.setCoords(); canvas.renderAll(); notify(); } },
        toggleItalic: () => { const o = canvas.getActiveObject(); if (o && o.set) { o.set('fontStyle', o.fontStyle === 'italic' ? 'normal' : 'italic'); o.initDimensions(); o.setCoords(); canvas.renderAll(); notify(); } },
        toggleUnderline: () => { const o = canvas.getActiveObject(); if (o && o.set) { o.set('underline', !o.underline); canvas.renderAll(); notify(); } },
        toggleLinethrough: () => { const o = canvas.getActiveObject(); if (o && o.set) { o.set('linethrough', !o.linethrough); canvas.renderAll(); notify(); } },
        toggleTextRectangle: () => {
            const activeObject = canvas.getActiveObject();
            if (!activeObject || (!activeObject.isType('textbox') && !activeObject.isType('i-text') && !activeObject.isType('text'))) return;

            if (activeObject.boundRect) {
                // Suppression du rectangle de fond sans toucher au texte
                canvas.remove(activeObject.boundRect);
                activeObject.boundRect = null;
                canvas.renderAll();
                notify();
            } else {
                // Ajout du rectangle de fond derrière le texte
                const padding = 10;
                const textWidth = activeObject.width * activeObject.scaleX;
                const textHeight = activeObject.height * activeObject.scaleY;

                const rect = new fabric.Rect({
                    left: activeObject.left - padding,
                    top: activeObject.top - padding,
                    width: textWidth + padding * 2,
                    height: textHeight + padding * 2,
                    fill: 'transparent', 
                    stroke: '#000000', 
                    strokeWidth: 2,
                    rx: 8,
                    ry: 8,
                    selectable: false,
                    evented: false
                });

                activeObject.boundRect = rect;
                canvas.add(rect);
                rect.sendToBack();
                canvas.renderAll();
                notify();
            }
        },
        setRectangleCornerRadius: (r) => {
            const o = canvas.getActiveObject();
            if (!o) return;
            if (o.isType('rect')) { o.set({ rx: r, ry: r }); canvas.renderAll(); notify(); }
        },
        centerHorizontal: () => { const o = canvas.getActiveObject(); if (o) { canvas.centerObjectH(o); o.setCoords(); canvas.renderAll(); notify(); } },
        centerVertical: () => { const o = canvas.getActiveObject(); if (o) { canvas.centerObjectV(o); o.setCoords(); canvas.renderAll(); notify(); } },
        centerBoth: () => { const o = canvas.getActiveObject(); if (o) { canvas.centerObject(o); o.setCoords(); canvas.renderAll(); notify(); } },
        invertSelected: () => {
            const activeObject = canvas.getActiveObject();
            if (!activeObject) return;

            if (activeObject.isType('image') || activeObject.cacheKey) {
                const filters = activeObject.filters || [];
                const idx = filters.findIndex(f => f && f.type === 'Invert');
                if (idx >= 0) activeObject.filters.splice(idx, 1); else activeObject.filters.push(new fabric.Image.filters.Invert());
                activeObject.applyFilters();
                canvas.renderAll();
                notify();
            } else if (activeObject.isType('textbox') || activeObject.isType('i-text') || activeObject.isType('text')) {
                // Gestion du texte avec ou sans boundRect
                if (activeObject.boundRect) {
                    const rect = activeObject.boundRect;
                    const isNormal = (rect.stroke === '#000000'); // Si contour noir, on inverse

                    rect.set({
                        fill: isNormal ? '#000000' : 'transparent',
                        stroke: isNormal ? '#ffffff' : '#000000'
                    });
                    activeObject.set({
                        fill: isNormal ? '#ffffff' : '#000000'
                    });
                } else {
                    const currentFill = activeObject.fill;
                    activeObject.set({
                        fill: currentFill === '#ffffff' ? '#000000' : '#ffffff'
                    });
                }
                canvas.renderAll();
                notify();
            } else if (activeObject.isType('rect')) {
                const currentFill = activeObject.fill;
                if (currentFill === 'transparent' || currentFill === '') {
                    activeObject.set({ fill: '#000000', stroke: '#ffffff' });
                } else {
                    activeObject.set({ fill: 'transparent', stroke: '#000000' });
                }
                canvas.renderAll();
                notify();
            }
        },
        save: () => {
            const json = canvas.toJSON();
            json.canvasWidth = canvas.width;
            json.canvasHeight = canvas.height;
            return JSON.stringify(json);
        },
        dispose: () => {
            window.removeEventListener('keydown', handleKeyDown);
            canvas.dispose();
        }
    };
}