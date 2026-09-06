"""Explicit M2 host-integration edits, applied only to staged upstream copies."""
from pathlib import Path

def adapt_host(output: Path, notice: str):
    main_path = output / 'js/main.js'
    text = main_path.read_text(encoding='utf-8')
    def replace(old, new):
        nonlocal text
        assert text.count(old) == 1, f'Unexpected upstream integration anchor: {old[:70]}'
        text = text.replace(old, new)
    replace('async function handleModelFile(file, stepSettings = null) {', "async function handleModelFile(file, stepSettings = null) {\n  window.dispatchEvent(new CustomEvent('meshme:before-model-load', {detail:{file}}));")
    replace("    updateSmartResBtnState();\n    updatePreview();\n  } catch (err) {",
            "    updateSmartResBtnState();\n    updatePreview();\n    window.dispatchEvent(new CustomEvent('meshme:model-result', { detail: { file, success: true } }));\n    return true;\n  } catch (err) {\n    window.dispatchEvent(new CustomEvent('meshme:model-result', { detail: { file, success: false, error: err?.message } }));")
    replace("      alert(t('alerts.loadFailed', { msg: err.message }));\n    }\n  } finally {",
            "      alert(t('alerts.loadFailed', { msg: err.message }));\n    }\n    return false;\n  } finally {")
    replace("function closeStepDialog() {\n  _stepEstimateSeq++;", "function closeStepDialog(accepted = false) {\n  if (_stepDialogFile && accepted !== true) window.dispatchEvent(new CustomEvent('meshme:model-result', { detail: { file: _stepDialogFile, success: false, error: 'STEP import cancelled' } }));\n  _stepEstimateSeq++;")
    replace("    closeStepDialog();\n    handleModelFile(file, settings);", "    closeStepDialog(true);\n    handleModelFile(file, settings);")
    replace("    if (choice === null) return; // dialog dismissed → load nothing", "    if (choice === null) return false; // dialog dismissed → load nothing")
    replace("    await handleModelFile(file);\n    return;\n  }\n\n  const unzipped", "    return await handleModelFile(file);\n  }\n\n  const unzipped")
    replace("      await handleModelFile(stlFile);", "      if (await handleModelFile(stlFile) === false) throw new Error('Project model could not be loaded.');")
    replace("    await _applyImportedTexture(unzipped, data);\n\n    _autoSaveSettings();\n  } finally {", "    await _applyImportedTexture(unzipped, data);\n\n    _autoSaveSettings();\n    return true;\n  } finally {")
    replace("function _downloadBlob(blob, filename) {", "function _downloadBlob(blob, filename) {\n  if (window.meshMe360) { void window.meshMe360.download(blob, filename); return; }")
    text += "\n// " + notice + "\nexport { handleModelFile as desktopLoadModel, importProject as desktopLoadProject, setViewerTheme as desktopSetTheme, setLang as desktopSetLanguage };\n"
    text += "\nexport function desktopSceneCapture() { return { geometry: currentGeometry?.clone(), settings: getSettingsSnapshot(), mask: _collectCurrentMask() }; }\nexport function desktopSceneActivate(geometry, snapshot, mask) { const name = currentStlName; const work = geometry.index ? geometry.toNonIndexed() : geometry; if (work !== geometry) geometry.dispose(); adoptBakedGeometry(work, computeBounds(work)); currentStlName = name; applySettingsSnapshot(snapshot); if (mask) _restoreMask(mask); }\n"
    main_path.write_text('// ' + notice + '\n' + text, encoding='utf-8', newline='\n')
    exporter_path = output / 'js/exporter.js'
    exporter = exporter_path.read_text(encoding='utf-8')
    exporter = exporter.replace("function triggerDownload(buffer, filename, mime = 'application/octet-stream') {", "function triggerDownload(buffer, filename, mime = 'application/octet-stream') {\n  if (filename === null) return buffer;")
    assert exporter.count('  triggerDownload(buffer, filename);') == 1
    exporter = exporter.replace('  triggerDownload(buffer, filename);', '  return triggerDownload(buffer, filename);')
    anchor = "  const blob = new Blob([buffer], { type: mime });"
    assert exporter.count(anchor) == 1
    exporter = exporter.replace(anchor, anchor + "\n  if (typeof window !== 'undefined' && window.meshMe360) { void window.meshMe360.download(blob, filename); return; }")
    exporter_path.write_text('// ' + notice + '\n' + exporter, encoding='utf-8', newline='\n')
    viewer_path = output / 'js/viewer.js'
    viewer = viewer_path.read_text(encoding='utf-8')
    anchor = '      renderer.render(scene, camera);'
    assert viewer.count(anchor) == 1
    viewer = viewer.replace(anchor, '      if (_desktopRender) _desktopRender(renderer, scene, camera);\n      else renderer.render(scene, camera);')
    viewer += '\n// Desktop presentation callback only; geometry algorithms remain preserved.\nlet _desktopRender = null;\nexport function setDesktopRender(callback) { _desktopRender = callback; requestRender(); }\n'
    viewer_path.write_text('// ' + notice + '\n' + viewer, encoding='utf-8', newline='\n')
