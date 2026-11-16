export async function initEditor(dotNetHelper, canvasId, initialJson, width, height) {
    // 0. Charger les polices Google Fonts dynamiquement si absentes
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
        fontLink.href = 'https://fonts.googleapis.com/css2?family=Inter:wght@400;700&family=JetBrains+Mono:wght@400;700&family=Lora:ital,wght@0,400;0,700;1,400&family=Share+Tech+Mono&display=swap';
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

    if (initialJson) {
        try {
            const parsed = typeof initialJson === 'string' ? JSON.parse(initialJson) : initialJson;
            canvas.loadFromJSON(parsed, () => canvas.renderAll());
        } catch (e) {
            console.error("Erreur parsing JSON", e);
        }
    }

    function notifySelection(obj) {
        if (!obj) {
            dotNetRef.invokeMethodAsync('OnObjectSelected', null);
            return;
        }

        let type = obj.type;
        // Si c'est un groupe, on peut affiner (ex: distinguer un texte avec cadre d'un autre groupe)
        if (type === 'group') {
            const items = obj.getObjects();
            if (items.some(item => item.isType('i-text') || item.isType('text'))) {
                type = 'text-rectangle';
            }
        }

        dotNetRef.invokeMethodAsync('OnObjectSelected', type);
    }

    const notify = () => {
        const json = JSON.stringify(canvas.toJSON());
        dotNetHelper.invokeMethodAsync('UpdateJsonData', json);
    };

    canvas.on('object:modified', notify);
    canvas.on('object:added', notify);
    canvas.on('object:removed', notify);

    canvas.on('selection:created', (e) => notifySelection(e.selected[0]));
    canvas.on('selection:updated', (e) => notifySelection(e.selected[0]));
    canvas.on('selection:cleared', () => notifySelection(null));

    return {
        addText: (text, fontFamily = 'Inter') => {
            const textObj = new fabric.IText(text, { 
                left: 20, 
                top: 20, 
                fontSize: 16, 
                fill: '#000000',
                fontFamily: fontFamily 
            });
            canvas.add(textObj);
            canvas.setActiveObject(textObj);
            notify();
        },
        addRectangle: () => {
            const rect = new fabric.Rect({
                left: 20, top: 20, width: 100, height: 50,
                fill: 'transparent', stroke: '#000000', strokeWidth: 1,
                rx: 6, ry: 6 // Coins arrondis par défaut
            });
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
                canvas.renderAll();
                notify();
            }
        },
        toggleBold: () => {
            const activeObject = canvas.getActiveObject();
            if (activeObject && (activeObject.isType('i-text') || activeObject.isType('text'))) {
                const isBold = activeObject.fontWeight === 'bold';
                activeObject.set('fontWeight', isBold ? 'normal' : 'bold');
                canvas.renderAll();
                notify();
            }
        },
        toggleItalic: () => {
            const activeObject = canvas.getActiveObject();
            if (activeObject && (activeObject.isType('i-text') || activeObject.isType('text'))) {
                const isItalic = activeObject.fontStyle === 'italic';
                activeObject.set('fontStyle', isItalic ? 'normal' : 'italic');
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

            // Vérifier si le texte a déjà un rectangle de contour associé
            if (activeObject.boundRect) {
                canvas.remove(activeObject.boundRect);
                activeObject.boundRect = null;
                canvas.renderAll();
                notify();
            } else {
                // Déterminer l'état actuel (lumière normale vs inversée)
                const isInverse = (activeObject.fill === '#ffffff' || activeObject.backgroundColor === '#000000');
                
                const fillColor = isInverse ? '#000000' : '#ffffff';
                const strokeColor = isInverse ? '#ffffff' : '#000000';

                // Appliquer les couleurs raccord au texte
                activeObject.set({
                    fill: isInverse ? '#ffffff' : '#000000',
                    backgroundColor: 'transparent'
                });

                // Utiliser exactement les dimensions réelles du texte avec un padding uniforme
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

            // 1. Si c'est un rectangle simple
            if (activeObject.isType('rect')) {
                activeObject.set({ rx: radius, ry: radius });
                canvas.renderAll();
                notify();
            } 
            // 2. Si c'est un groupe de texte avec cadre
            else if (activeObject.isType('group')) {
                const items = activeObject.getObjects();
                const rectObj = items.find(obj => obj.isType('rect'));
                if (rectObj) {
                    rectObj.set({ rx: radius, ry: radius });
                    canvas.renderAll();
                    notify();
                }
            }
        },
        invertSelected: () => {
            const activeObject = canvas.getActiveObject();
            if (!activeObject) return;

            // 1. Si c'est une image (Code-barres ou QR code)
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
            } 
            // 2. Si c'est un texte
            else if (activeObject.isType('i-text') || activeObject.isType('text')) {
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
            } 
            // 3. Si c'est un rectangle ou une forme
            else if (activeObject.isType('rect')) {
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
        dispose: () => canvas.dispose()
    };
}