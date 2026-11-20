async function loadExternalResources() {
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

function configureObjectControls(obj) {
    if (!obj) return;
    obj.set('lockRotation', true);
    obj.setControlVisible('mtr', false);

    if (obj.isType('i-text') || obj.isType('text') || obj.isType('textbox')) {
        obj.setControlsVisibility({
            mtr: false,
            tl: true,
            tr: true,
            bl: true,
            br: true,
            ml: true,
            mr: true,
            mt: false,
            mb: false
        });
    } else if (obj.isType('line')) {
        obj.set({
            padding: 12,
            selectable: true,
            evented: true
        });
        obj.setControlsVisibility({
            mtr: false,
            tl: false,
            tr: false,
            bl: false,
            br: false,
            mt: false,
            mb: false,
            ml: true,
            mr: true
        });
    }
}

const ALLOWED_SIZES = [12, 14, 16, 18, 24, 28, 32, 40, 48, 56, 64, 72, 96, 112, 128, 144, 168, 192, 224, 256];

function getClosestAllowedSize(rawSize) {
    return ALLOWED_SIZES.reduce((prev, curr) =>
        Math.abs(curr - rawSize) < Math.abs(prev - rawSize) ? curr : prev
    );
}

export async function initEditor(dotNetHelper, canvasId, initialJson, width, height, meta = null) {
    await loadExternalResources();

    const canvas = new fabric.Canvas(canvasId, {
        width: width,
        height: height,
        backgroundColor: '#ffffff'
    });

    let labelWidthMm = meta && typeof meta.widthMm === 'number' ? meta.widthMm : Number((width * 25.4 / (meta && meta.printerDpi ? meta.printerDpi : 203)).toFixed(2));
    let labelHeightMm = meta && typeof meta.heightMm === 'number' ? meta.heightMm : Number((height * 25.4 / (meta && meta.printerDpi ? meta.printerDpi : 203)).toFixed(2));
    let printerDpi = meta && typeof meta.printerDpi === 'number' ? meta.printerDpi : 203;
    let isLoadingInitialJson = false;

    function buildPayload() {
        const json = canvas.toJSON();
        json.canvasWidth = canvas.width;
        json.canvasHeight = canvas.height;
        json.widthMm = labelWidthMm;
        json.heightMm = labelHeightMm;
        json.printerDpi = printerDpi;
        return JSON.stringify(json);
    }

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
        if (isLoadingInitialJson) return;
        const payload = buildPayload();
        dotNetHelper.invokeMethodAsync('UpdateJsonData', payload);
    };

    if (initialJson) {
        try {
            const parsed = typeof initialJson === 'string' ? JSON.parse(initialJson) : initialJson;
            if (typeof parsed.widthMm === 'number') labelWidthMm = parsed.widthMm;
            if (typeof parsed.heightMm === 'number') labelHeightMm = parsed.heightMm;
            if (typeof parsed.printerDpi === 'number') printerDpi = parsed.printerDpi;

            isLoadingInitialJson = true;
            canvas.loadFromJSON(parsed, () => {
                canvas.getObjects().forEach(obj => {
                    configureObjectControls(obj);
                    if (obj.isType('image')) {
                        obj.set('imageSmoothing', false);
                    } else if (obj.isType('group')) {
                        obj.getObjects().forEach(child => {
                            if (child.isType('image')) {
                                child.set('imageSmoothing', false);
                            }
                        });
                    }
                });
                isLoadingInitialJson = false;
                canvas.renderAll();
            });
        } catch (e) {
            isLoadingInitialJson = false;
            console.error("Erreur parsing JSON", e);
        }
    }

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

    canvas.on('object:modified', (e) => {
        const obj = e.target;
        if (!obj) return;

        if (obj.isType('i-text') || obj.isType('text') || obj.isType('textbox')) {
            const scaleX = obj.scaleX || 1;
            const scaleY = obj.scaleY || 1;
            const scale = Math.max(scaleX, scaleY);
            const snappedSize = getClosestAllowedSize((obj.fontSize || 28) * scale);
            const nextWidth = obj.isType('textbox') ? Math.max(20, (obj.width || 100) * scaleX) : obj.width;

            obj.set({
                fontSize: snappedSize,
                width: nextWidth,
                scaleX: 1,
                scaleY: 1
            });
            obj.initDimensions();
            obj.setCoords();

            if (obj.boundRect) {
                const padding = 10;
                obj.boundRect.set({
                    left: obj.left - padding,
                    top: obj.top - padding,
                    width: obj.width + padding * 2,
                    height: obj.height + padding * 2,
                    scaleX: 1,
                    scaleY: 1
                });
                obj.boundRect.setCoords();
            }
        } else if (obj.customType === 'barcode-group' && obj.isType('group')) {
            const sx = obj.scaleX || 1;
            const sy = obj.scaleY || 1;
            const items = obj.getObjects();
            const barcodeImg = items.find(i => i.isType('image'));
            const barcodeTxt = items.find(i => i.isType('text') || i.isType('textbox') || i.isType('i-text'));

            if (barcodeImg && barcodeTxt) {
                const realBarWidth = barcodeImg.width * barcodeImg.scaleX * sx;
                const realBarHeight = barcodeImg.height * barcodeImg.scaleY * sy;
                const baseBarWidth = barcodeImg.baseBarWidth || barcodeImg.width || 1;
                const baseFontSize = barcodeTxt.baseFontSize || 28;

                const widthRatio = realBarWidth / baseBarWidth;
                const newFontSize = Math.max(10, Math.round(baseFontSize * widthRatio));
                const gap = Math.max(4, Math.round(6 * widthRatio));

                barcodeTxt.set({
                    fontSize: newFontSize,
                    scaleX: 1 / sx,
                    scaleY: 1 / sy
                });

                const realTextHeight = barcodeTxt.height;
                const totalRealHeight = realBarHeight + gap + realTextHeight;

                barcodeImg.set({
                    left: 0,
                    top: -(totalRealHeight / 2) / sy,
                    originX: 'center',
                    originY: 'top',
                    imageSmoothing: false
                });

                barcodeTxt.set({
                    left: 0,
                    top: (-(totalRealHeight / 2) + realBarHeight + gap) / sy,
                    originX: 'center',
                    originY: 'top'
                });

                obj.addWithUpdate();
                obj.setCoords();
            }
        } else if (obj.isType('rect')) {
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

    canvas.on('object:added', (e) => {
        const obj = e.target;
        if (!obj || obj.excludeFromExport) return;
        configureObjectControls(obj);
        notify();
    });

    canvas.on('object:removed', (e) => {
        const obj = e.target;
        if (!obj || obj.excludeFromExport) return;
        notify();
    });

    canvas.on('selection:created', (e) => notifySelection(e.selected[0]));
    canvas.on('selection:updated', (e) => notifySelection(e.selected[0]));
    canvas.on('selection:cleared', () => notifySelection(null));

    canvas.on('object:moving', (e) => {
        const obj = e.target;
        if (obj === alignVerticalLine || obj === alignHorizontalLine) return;

        const objCenterX = obj.left + (obj.width * obj.scaleX) / 2;
        const objCenterY = obj.top + (obj.height * obj.scaleY) / 2;

        hideGuides();

        if (Math.abs(objCenterX - canvas.width / 2) < snappingDistance) {
            obj.set({ left: canvas.width / 2 - (obj.width * obj.scaleX) / 2 });
            showGuide(true, canvas.width / 2);
        }

        if (Math.abs(objCenterY - canvas.height / 2) < snappingDistance) {
            obj.set({ top: canvas.height / 2 - (obj.height * obj.scaleY) / 2 });
            showGuide(false, canvas.height / 2);
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

    canvas.on('mouse:up', () => {
        hideGuides();
        canvas.renderAll();
    });

    let _clipboard = null;
    const handleKeyDown = (e) => {
        const activeObj = canvas.getActiveObject();
        if (['INPUT', 'TEXTAREA', 'SELECT'].includes(e.target.tagName) || (activeObj && activeObj.isEditing)) return;

        if (activeObj && ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.key)) {
            e.preventDefault();
            const step = (e.ctrlKey || e.metaKey) ? 1 : 5;
            let dx = 0;
            let dy = 0;

            if (e.key === 'ArrowLeft') dx = -step;
            else if (e.key === 'ArrowRight') dx = step;
            else if (e.key === 'ArrowUp') dy = -step;
            else if (e.key === 'ArrowDown') dy = step;

            const activeObjects = canvas.getActiveObjects();
            activeObjects.forEach(obj => {
                obj.set({
                    left: (obj.left || 0) + dx,
                    top: (obj.top || 0) + dy
                });
                obj.setCoords();

                if (obj.boundRect) {
                    obj.boundRect.set({
                        left: (obj.boundRect.left || 0) + dx,
                        top: (obj.boundRect.top || 0) + dy
                    });
                    obj.boundRect.setCoords();
                }
            });

            canvas.renderAll();
            notify();
            return;
        }

        if ((e.key === 'Delete' || e.key === 'Backspace') && activeObj) {
            e.preventDefault();
            const activeObjects = canvas.getActiveObjects();
            if (activeObjects.length) {
                activeObjects.forEach(obj => { if (obj.boundRect) canvas.remove(obj.boundRect); });
                canvas.remove(...activeObjects);
                canvas.discardActiveObject();
                canvas.renderAll();
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
            });
        }
    };
    window.addEventListener('keydown', handleKeyDown);

    return {
        addText: (text, fontFamily = 'Inter') => {
            const textObj = new fabric.Textbox(text, {
                left: 20, top: 20, width: 180, fontSize: 18, scaleX: 1, scaleY: 1, fill: '#000000', fontFamily: fontFamily, splitByGrapheme: true
            });
            configureObjectControls(textObj);
            canvas.add(textObj);
            canvas.setActiveObject(textObj);
        },
        addLine: () => {
            const line = new fabric.Line([50, 50, 250, 50], {
                stroke: '#000000',
                strokeWidth: 2,
                padding: 12,
                scaleX: 1,
                scaleY: 1,
                selectable: true,
                evented: true
            });
            configureObjectControls(line);
            canvas.add(line);
            canvas.setActiveObject(line);
            canvas.renderAll();
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
        },
        addBarcode: (barcodeData) => {
            try {
                const value = barcodeData || "12345678";
                const tempCanvas = document.createElement('canvas');
                JsBarcode(tempCanvas, value, {
                    format: "CODE128",
                    lineColor: "#000000",
                    background: "#ffffff",
                    width: 4,
                    height: 140,
                    margin: 0,
                    displayValue: false
                });
                fabric.Image.fromURL(tempCanvas.toDataURL("image/png"), (img) => {
                    img.set({
                        left: 0,
                        top: 0,
                        originX: 'center',
                        originY: 'top',
                        scaleX: 1,
                        scaleY: 1,
                        imageSmoothing: false,
                        customType: 'barcode',
                        barcodeValue: value,
                        baseBarWidth: img.width
                    });

                    const baseFontSize = 28;
                    const labelText = new fabric.Text(value, {
                        left: 0,
                        top: img.height + 6,
                        originX: 'center',
                        originY: 'top',
                        fontSize: baseFontSize,
                        fontFamily: 'JetBrains Mono, monospace',
                        textAlign: 'center',
                        fill: '#000000',
                        scaleX: 1,
                        scaleY: 1,
                        baseFontSize: baseFontSize
                    });

                    const group = new fabric.Group([img, labelText], {
                        left: 40,
                        top: 40,
                        scaleX: 1,
                        scaleY: 1,
                        customType: 'barcode-group',
                        barcodeValue: value
                    });

                    configureObjectControls(group);
                    canvas.add(group);
                    canvas.setActiveObject(group);
                    canvas.renderAll();
                });
            } catch (e) { console.error(e); }
        },
        addQrCode: (textData) => {
            try {
                const value = textData || "https://fastlabel";
                const qr = qrcode(4, 'M');
                qr.addData(value);
                qr.make();
                const count = qr.getModuleCount(), tileSize = 12, margin = 24, size = count * tileSize + margin * 2;
                const tempCanvas = document.createElement('canvas');
                tempCanvas.width = size; tempCanvas.height = size;
                const ctx = tempCanvas.getContext('2d');
                ctx.imageSmoothingEnabled = false;
                ctx.fillStyle = '#ffffff'; ctx.fillRect(0, 0, size, size);
                ctx.fillStyle = '#000000';
                for (let r = 0; r < count; r++) {
                    for (let c = 0; c < count; c++) {
                        if (qr.isDark(r, c)) ctx.fillRect(margin + c * tileSize, margin + r * tileSize, tileSize, tileSize);
                    }
                }
                fabric.Image.fromURL(tempCanvas.toDataURL("image/png"), (img) => {
                    img.set({
                        left: 40,
                        top: 40,
                        scaleX: 1,
                        scaleY: 1,
                        imageSmoothing: false,
                        customType: 'qrcode',
                        qrValue: value
                    });
                    configureObjectControls(img);
                    canvas.add(img);
                    canvas.setActiveObject(img);
                    canvas.renderAll();
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
        setLabelMetadata: (wMm, hMm, dpi) => {
            if (typeof wMm === 'number' && wMm > 0) labelWidthMm = wMm;
            if (typeof hMm === 'number' && hMm > 0) labelHeightMm = hMm;
            if (typeof dpi === 'number' && dpi > 0) printerDpi = dpi;
        },
        resizeCanvas: (w, h, wMm = null, hMm = null, dpi = null) => {
            if (typeof wMm === 'number' && wMm > 0) labelWidthMm = wMm;
            if (typeof hMm === 'number' && hMm > 0) labelHeightMm = hMm;
            if (typeof dpi === 'number' && dpi > 0) printerDpi = dpi;
            canvas.setWidth(w);
            canvas.setHeight(h);
            canvas.calcOffset();
            canvas.renderAll();
            notify();
        },
        recalcOffset: () => {
            canvas.calcOffset();
            canvas.renderAll();
        },
        deleteSelected: () => {
            const activeObjects = canvas.getActiveObjects();
            if (activeObjects.length) {
                activeObjects.forEach(obj => { if (obj.boundRect) canvas.remove(obj.boundRect); });
                canvas.remove(...activeObjects);
                canvas.discardActiveObject();
                canvas.renderAll();
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
                canvas.remove(activeObject.boundRect);
                activeObject.boundRect = null;
                canvas.renderAll();
                notify();
            } else {
                const padding = 10;
                const textWidth = activeObject.width;
                const textHeight = activeObject.height;

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
                if (activeObject.boundRect) {
                    const rect = activeObject.boundRect;
                    const isNormal = (rect.stroke === '#000000');

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
            const payload = buildPayload();
            notify();
            return payload;
        },
        dispose: () => {
            window.removeEventListener('keydown', handleKeyDown);
            canvas.dispose();
        }
    };
}