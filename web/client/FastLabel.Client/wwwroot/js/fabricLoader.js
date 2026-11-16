export async function initEditor(dotNetHelper, canvasId, initialJson, width, height) {
    // 0. Charger les polices Google Fonts dynamiquement si absentes (avec tous les styles et italiques)
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

    // 1. Charger Fabric.js si absent
    if (typeof fabric === 'undefined') {
        await new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = 'https://cdnjs.cloudflare.com/ajax/libs/fabric.js/5.3.1/fabric.min.js';
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });
    }

    // 2. Charger qrcode-generator si absent
    if (typeof qrcode === 'undefined') {
        await new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = 'https://cdnjs.cloudflare.com/ajax/libs/qrcode-generator/1.4.4/qrcode.min.js';
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });
    }

    // 3. Charger JsBarcode si absent
    if (typeof JsBarcode === 'undefined') {
        await new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = 'https://cdnjs.cloudflare.com/ajax/libs/jsbarcode/3.11.5/JsBarcode.all.min.js';
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });
    }

    const canvas = new fabric.Canvas(canvasId, {
        width: width,
        height: height,
        backgroundColor: '#ffffff'
    });

    // Fonction centralisée pour verrouiller la rotation et les poignées d'étirement texte
    function configureObjectControls(obj) {
        if (!obj) return;
        obj.set('lockRotation', true);
        obj.setControlVisible('mtr', false);

        if (obj.isType('i-text') || obj.isType('text')) {
            obj.setControlsVisibility({
                mtr: false,
                ml: false,
                mr: false,
                mt: false,
                mb: false
            });
        }
    }

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

    function notifySelection(obj) {
        if (!obj) {
            dotNetHelper.invokeMethodAsync('OnObjectSelected', null);
            return;
        }

        let type = obj.type;
        if (type === 'group') {
            const items = obj.getObjects();
            if (items.some(item => item.isType('i-text') || item.isType('text'))) {
                type = 'text-rectangle';
            }
        }

        dotNetHelper.invokeMethodAsync('OnObjectSelected', type);
    }

    const notify = () => {
        const json = JSON.stringify(canvas.toJSON());
        dotNetHelper.invokeMethodAsync('UpdateJsonData', json);
    };

    // Paliers de tailles autorisés pour correspondre exactement aux .ttf du backend
    const ALLOWED_SIZES = [12, 14, 16, 18, 24, 28, 32, 40, 48, 56, 64, 72, 96];
    function getClosestAllowedSize(rawSize) {
        return ALLOWED_SIZES.reduce((prev, curr) =>
            Math.abs(curr - rawSize) < Math.abs(prev - rawSize) ? curr : prev
        );
    }

    canvas.on('object:modified', (e) => {
        const obj = e.target;

        // Verrouillage / Snap de la taille de police pour aligner le front et le back (.NET)
        if (obj && (obj.isType('i-text') || obj.isType('text'))) {
            const scale = Math.max(obj.scaleX, obj.scaleY);
            const effectiveSize = obj.fontSize * scale;
            const snappedSize = getClosestAllowedSize(effectiveSize);

            obj.set({
                fontSize: snappedSize,
                scaleX: 1,
                scaleY: 1
            });

            // Force Fabric.js à recalculer la boîte de sélection bleue
            obj.initDimensions();
            obj.setCoords();

            if (obj.boundRect) {
                const padding = 4;
                obj.boundRect.set({
                    left: obj.left - padding,
                    top: obj.top - padding,
                    width: obj.width + padding * 2,
                    height: obj.height + padding * 2
                });
                obj.boundRect.setCoords();
            }
            canvas.renderAll();
        }

        notify();
    });

    canvas.on('object:added', (e) => {
        configureObjectControls(e.target);
        notify();
    });

    canvas.on('object:removed', notify);

    canvas.on('selection:created', (e) => notifySelection(e.selected[0]));
    canvas.on('selection:updated', (e) => notifySelection(e.selected[0]));
    canvas.on('selection:cleared', () => notifySelection(null));

    // --- MAGNÉTISME ET GUIDES D'ALIGNEMENT (SNAPPING) ---
    const snappingDistance = 5;
    let alignVerticalLine = null;
    let alignHorizontalLine = null;

    function showVerticalGuide(x) {
        if (!alignVerticalLine) {
            alignVerticalLine = new fabric.Line([x, 0, x, canvas.height], {
                stroke: 'rgba(255, 0, 0, 0.6)',
                strokeWidth: 1,
                selectable: false,
                evented: false,
                excludeFromExport: true
            });
            canvas.add(alignVerticalLine);
        } else {
            alignVerticalLine.set({ x1: x, x2: x, y2: canvas.height });
            canvas.bringToFront(alignVerticalLine);
            alignVerticalLine.set('visible', true);
        }
    }

    function showHorizontalGuide(y) {
        if (!alignHorizontalLine) {
            alignHorizontalLine = new fabric.Line([0, y, canvas.width, y], {
                stroke: 'rgba(255, 0, 0, 0.6)',
                strokeWidth: 1,
                selectable: false,
                evented: false,
                excludeFromExport: true
            });
            canvas.add(alignHorizontalLine);
        } else {
            alignHorizontalLine.set({ y1: y, y2: y, x2: canvas.width });
            canvas.bringToFront(alignHorizontalLine);
            alignHorizontalLine.set('visible', true);
        }
    }

    function hideGuides() {
        if (alignVerticalLine) alignVerticalLine.set('visible', false);
        if (alignHorizontalLine) alignHorizontalLine.set('visible', false);
    }

    canvas.on('object:moving', (e) => {
        const obj = e.target;
        const canvasWidth = canvas.width;
        const canvasHeight = canvas.height;

        const objCenterX = obj.left + (obj.width * obj.scaleX) / 2;
        const objCenterY = obj.top + (obj.height * obj.scaleY) / 2;

        let snappedH = false;
        let snappedV = false;

        hideGuides();

        if (Math.abs(objCenterX - canvasWidth / 2) < snappingDistance) {
            obj.set({ left: canvasWidth / 2 - (obj.width * obj.scaleX) / 2 });
            showVerticalGuide(canvasWidth / 2);
            snappedH = true;
        }

        if (Math.abs(objCenterY - canvasHeight / 2) < snappingDistance) {
            obj.set({ top: canvasHeight / 2 - (obj.height * obj.scaleY) / 2 });
            showHorizontalGuide(canvasHeight / 2);
            snappedV = true;
        }

        if (obj.boundRect) {
            const padding = 4;
            obj.boundRect.set({
                left: obj.left - padding,
                top: obj.top - padding
            });
            obj.boundRect.setCoords();
        }

        if (!snappedH && alignVerticalLine) alignVerticalLine.set('visible', false);
        if (!snappedV && alignHorizontalLine) alignHorizontalLine.set('visible', false);

        canvas.renderAll();
    });

    canvas.on('mouse:up', () => {
        hideGuides();
        canvas.renderAll();
    });

    // --- GESTION DES RACCOURCIS CLAVIER ---
    let _clipboard = null;

    const handleKeyDown = (e) => {
        const activeObj = canvas.getActiveObject();

        if (
            e.target.tagName === 'INPUT' ||
            e.target.tagName === 'TEXTAREA' ||
            e.target.tagName === 'SELECT' ||
            (activeObj && activeObj.isEditing)
        ) {
            return;
        }

        if ((e.key === 'Delete' || e.key === 'Backspace') && activeObj) {
            e.preventDefault();
            const activeObjects = canvas.getActiveObjects();
            if (activeObjects.length) {
                activeObjects.forEach(obj => {
                    if (obj.boundRect) canvas.remove(obj.boundRect);
                });
                canvas.remove(...activeObjects);
                canvas.discardActiveObject();
                canvas.renderAll();
                notify();
            }
        }

        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'c' && activeObj) {
            e.preventDefault();
            activeObj.clone((cloned) => {
                _clipboard = cloned;
            });
        }

        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'v' && _clipboard) {
            e.preventDefault();
            _clipboard.clone((clonedObj) => {
                canvas.discardActiveObject();

                clonedObj.set({
                    left: clonedObj.left + 15,
                    top: clonedObj.top + 15,
                    evented: true,
                });

                if (clonedObj.type === 'activeSelection') {
                    clonedObj.canvas = canvas;
                    clonedObj.forEachObject((obj) => {
                        configureObjectControls(obj);
                        canvas.add(obj);
                    });
                    clonedObj.setCoords();
                } else {
                    configureObjectControls(clonedObj);
                    canvas.add(clonedObj);
                }

                _clipboard.top += 15;
                _clipboard.left += 15;

                canvas.setActiveObject(clonedObj);
                canvas.requestRenderAll();
                notify();
            });
        }
    };

    window.addEventListener('keydown', handleKeyDown);

    return {
        addText: (text, fontFamily = 'Inter') => {
            const textObj = new fabric.IText(text, {
                left: 20,
                top: 20,
                fontSize: 18,
                fill: '#000000',
                fontFamily: fontFamily
            });
            configureObjectControls(textObj);
            canvas.add(textObj);
            canvas.setActiveObject(textObj);
            notify();
        },
        addRectangle: () => {
            const rect = new fabric.Rect({
                left: 20, top: 20, width: 100, height: 50,
                fill: 'transparent', stroke: '#000000', strokeWidth: 1,
                rx: 6, ry: 6
            });
            configureObjectControls(rect);
            canvas.add(rect);
            canvas.setActiveObject(rect);
            notify();
        },
        addBarcode: (barcodeData) => {
            try {
                const tempCanvas = document.createElement('canvas');
                JsBarcode(tempCanvas, barcodeData || "12345678", {
                    format: "CODE128",
                    lineColor: "#000",
                    width: 2,
                    height: 50,
                    displayValue: true
                });

                const imgUrl = tempCanvas.toDataURL("image/png");
                fabric.Image.fromURL(imgUrl, (img) => {
                    img.set({ left: 20, top: 20, scaleX: 0.8, scaleY: 0.8 });
                    configureObjectControls(img);
                    canvas.add(img);
                    canvas.setActiveObject(img);
                    canvas.renderAll();
                    notify();
                });
            } catch (e) {
                console.error("Erreur génération Code-barres", e);
            }
        },
        addImage: (url) => {
            fabric.Image.fromURL(url, (img) => {
                img.set({
                    left: 50,
                    top: 50,
                    scaleX: 0.5,
                    scaleY: 0.5
                });
                configureObjectControls(img);
                canvas.add(img);
                canvas.setActiveObject(img);
                canvas.renderAll();
                notify();
            }, { crossOrigin: 'anonymous' });
        },
        addQrCode: (textData) => {
            try {
                const typeNumber = 4;
                const errorCorrectionLevel = 'M';
                const qr = qrcode(typeNumber, errorCorrectionLevel);
                qr.addData(textData || "https://fastlabel");
                qr.make();

                const count = qr.getModuleCount();
                const tileSize = 6;
                const margin = 12;
                const size = count * tileSize + margin * 2;

                const tempCanvas = document.createElement('canvas');
                tempCanvas.width = size;
                tempCanvas.height = size;
                const ctx = tempCanvas.getContext('2d');

                ctx.fillStyle = '#ffffff';
                ctx.fillRect(0, 0, size, size);

                ctx.fillStyle = '#000000';
                for (let r = 0; r < count; r++) {
                    for (let c = 0; c < count; c++) {
                        if (qr.isDark(r, c)) {
                            ctx.fillRect(margin + c * tileSize, margin + r * tileSize, tileSize, tileSize);
                        }
                    }
                }

                const imgUrl = tempCanvas.toDataURL("image/png");
                fabric.Image.fromURL(imgUrl, (img) => {
                    img.set({
                        left: 20,
                        top: 20,
                        scaleX: 1,
                        scaleY: 1
                    });
                    configureObjectControls(img);
                    canvas.add(img);
                    canvas.setActiveObject(img);
                    canvas.renderAll();
                    notify();
                });
            } catch (e) {
                console.error("Erreur génération QR Code", e);
            }
        },
        resizeCanvas: (newWidth, newHeight) => {
            canvas.setWidth(newWidth);
            canvas.setHeight(newHeight);
            canvas.renderAll();
            notify();
        },
        deleteSelected: () => {
            const activeObjects = canvas.getActiveObjects();
            if (activeObjects.length) {
                activeObjects.forEach(obj => {
                    if (obj.boundRect) canvas.remove(obj.boundRect);
                });
                canvas.remove(...activeObjects);
                canvas.discardActiveObject();
                canvas.renderAll();
                notify();
            }
        },
        bringForward: () => {
            const activeObject = canvas.getActiveObject();
            if (activeObject) {
                canvas.bringForward(activeObject);
                canvas.renderAll();
                notify();
            }
        },
        sendBackwards: () => {
            const activeObject = canvas.getActiveObject();
            if (activeObject) {
                canvas.sendBackwards(activeObject);
                canvas.renderAll();
                notify();
            }
        },
        setFontFamily: (fontFamily) => {
            const activeObject = canvas.getActiveObject();
            if (activeObject && (activeObject.isType('i-text') || activeObject.isType('text'))) {
                activeObject.set('fontFamily', fontFamily);
                activeObject.initDimensions();
                activeObject.setCoords();
                canvas.renderAll();
                notify();
            }
        },
        toggleBold: () => {
            const activeObject = canvas.getActiveObject();
            if (activeObject && (activeObject.isType('i-text') || activeObject.isType('text'))) {
                const isBold = activeObject.fontWeight === 'bold';
                activeObject.set('fontWeight', isBold ? 'normal' : 'bold');
                activeObject.initDimensions();
                activeObject.setCoords();
                canvas.renderAll();
                notify();
            }
        },
        toggleItalic: () => {
            const activeObject = canvas.getActiveObject();
            if (activeObject && (activeObject.isType('i-text') || activeObject.isType('text'))) {
                const isItalic = activeObject.fontStyle === 'italic';
                activeObject.set('fontStyle', isItalic ? 'normal' : 'italic');
                activeObject.initDimensions();
                activeObject.setCoords();
                canvas.renderAll();
                notify();
            }
        },
        toggleUnderline: () => {
            const activeObject = canvas.getActiveObject();
            if (activeObject && (activeObject.isType('i-text') || activeObject.isType('text'))) {
                activeObject.set('underline', !activeObject.underline);
                canvas.renderAll();
                notify();
            }
        },
        toggleLinethrough: () => {
            const activeObject = canvas.getActiveObject();
            if (activeObject && (activeObject.isType('i-text') || activeObject.isType('text'))) {
                activeObject.set('linethrough', !activeObject.linethrough);
                canvas.renderAll();
                notify();
            }
        },
        toggleTextRectangle: () => {
            const activeObject = canvas.getActiveObject();
            if (!activeObject || (!activeObject.isType('i-text') && !activeObject.isType('text'))) return;

            if (activeObject.boundRect) {
                canvas.remove(activeObject.boundRect);
                activeObject.boundRect = null;
                canvas.renderAll();
                notify();
            } else {
                const isInverse = (activeObject.fill === '#ffffff' || activeObject.backgroundColor === '#000000');

                const fillColor = isInverse ? '#000000' : '#ffffff';
                const strokeColor = isInverse ? '#ffffff' : '#000000';

                activeObject.set({
                    fill: isInverse ? '#ffffff' : '#000000',
                    backgroundColor: 'transparent'
                });

                const padding = 4;
                const width = activeObject.width * activeObject.scaleX + padding * 2;
                const height = activeObject.height * activeObject.scaleY + padding * 2;

                const rect = new fabric.Rect({
                    left: activeObject.left - padding,
                    top: activeObject.top - padding,
                    width: width,
                    height: height,
                    fill: fillColor,
                    stroke: strokeColor,
                    strokeWidth: 2,
                    selectable: false,
                    evented: false
                });

                activeObject.boundRect = rect;

                activeObject.off('moving');
                activeObject.on('moving', () => {
                    rect.set({
                        left: activeObject.left - padding,
                        top: activeObject.top - padding
                    });
                });

                canvas.add(rect);
                rect.sendToBack();
                canvas.renderAll();
                notify();
            }
        },
        setRectangleCornerRadius: (radius) => {
            const activeObject = canvas.getActiveObject();
            if (!activeObject) return;

            if (activeObject.isType('rect')) {
                activeObject.set({ rx: radius, ry: radius });
                canvas.renderAll();
                notify();
            } else if (activeObject.isType('group')) {
                const items = activeObject.getObjects();
                const rectObj = items.find(obj => obj.isType('rect'));
                if (rectObj) {
                    rectObj.set({ rx: radius, ry: radius });
                    canvas.renderAll();
                    notify();
                }
            }
        },
        centerHorizontal: () => {
            const activeObject = canvas.getActiveObject();
            if (!activeObject) return;

            canvas.centerObjectH(activeObject);
            activeObject.setCoords();

            if (activeObject.boundRect) {
                const padding = 4;
                activeObject.boundRect.set({
                    left: activeObject.left - padding,
                    top: activeObject.top - padding
                });
                activeObject.boundRect.setCoords();
            }

            canvas.renderAll();
            notify();
        },
        centerVertical: () => {
            const activeObject = canvas.getActiveObject();
            if (!activeObject) return;

            canvas.centerObjectV(activeObject);
            activeObject.setCoords();

            if (activeObject.boundRect) {
                const padding = 4;
                activeObject.boundRect.set({
                    left: activeObject.left - padding,
                    top: activeObject.top - padding
                });
                activeObject.boundRect.setCoords();
            }

            canvas.renderAll();
            notify();
        },
        centerBoth: () => {
            const activeObject = canvas.getActiveObject();
            if (!activeObject) return;

            canvas.centerObject(activeObject);
            activeObject.setCoords();

            if (activeObject.boundRect) {
                const padding = 4;
                activeObject.boundRect.set({
                    left: activeObject.left - padding,
                    top: activeObject.top - padding
                });
                activeObject.boundRect.setCoords();
            }

            canvas.renderAll();
            notify();
        },
        invertSelected: () => {
            const activeObject = canvas.getActiveObject();
            if (!activeObject) return;

            if (activeObject.isType('image') || activeObject.cacheKey) {
                const filters = activeObject.filters || [];
                const invertIndex = filters.findIndex(f => f && f.type === 'Invert');
                if (invertIndex >= 0) {
                    activeObject.filters.splice(invertIndex, 1);
                } else {
                    activeObject.filters.push(new fabric.Image.filters.Invert());
                }
                activeObject.applyFilters();
                canvas.renderAll();
                notify();
            } else if (activeObject.isType('i-text') || activeObject.isType('text')) {
                const currentFill = activeObject.fill;

                if (currentFill === '#ffffff') {
                    activeObject.set({
                        fill: '#000000',
                        backgroundColor: 'transparent'
                    });
                } else {
                    activeObject.set({
                        fill: '#ffffff',
                        backgroundColor: '#000000'
                    });
                }
                canvas.renderAll();
                notify();
            } else if (activeObject.isType('rect')) {
                const currentFill = activeObject.fill;

                if (currentFill === '#000000') {
                    activeObject.set({
                        fill: 'transparent',
                        stroke: '#000000'
                    });
                } else {
                    activeObject.set({
                        fill: '#000000',
                        stroke: '#ffffff'
                    });
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