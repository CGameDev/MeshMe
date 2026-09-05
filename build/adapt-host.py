"""Explicit M2 host-integration edits, applied only to staged upstream copies."""
from pathlib import Path

def adapt_host(output: Path, notice: str):
    main_path = output / 'js/main.js'
    text = main_path.read_text(encoding='utf-8')
    def replace(old, new):
        nonlocal text
        assert text.count(old) == 1, f'Unexpected upstream integration anchor: {old[:70]}'
        text = text.replace(old, new)
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
    main_path.write_text('// ' + notice + '\n' + text, encoding='utf-8', newline='\n')
    exporter_path = output / 'js/exporter.js'
    exporter = exporter_path.read_text(encoding='utf-8')
    anchor = "  const blob = new Blob([buffer], { type: mime });"
    assert exporter.count(anchor) == 1
    exporter = exporter.replace(anchor, anchor + "\n  if (typeof window !== 'undefined' && window.meshMe360) { void window.meshMe360.download(blob, filename); return; }")
    exporter_path.write_text('// ' + notice + '\n' + exporter, encoding='utf-8', newline='\n')
