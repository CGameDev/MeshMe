// SPDX-License-Identifier: AGPL-3.0-only
/** Serializes and restores recovery-compatible project state without marking it saved. */
import {serializeProject,deserializeProject} from './scene-document.js';
// M3 format contract for the M6 host coordinator. Timing, retention, disk paths
// and crash-session decisions belong to that coordinator, not this serializer.
/** Return no snapshot for clean or busy state; otherwise serialize without acknowledging a save. */
export function createRecoverySnapshot(document,{processing=false}={}){
 if(processing||!document.dirty)return null;
 return serializeProject(document.state);
}
/** Parse before replacement and restore the recovered document as dirty with fresh history. */
export function restoreRecoverySnapshot(document,bytes){
 // Parse and validate before changing any currently valid state.
 const recovered=deserializeProject(bytes);
 document.state=recovered;document.undoStack=[];document.redoStack=[];
 document.revision=document.nextRevision++;document.savedRevision=-1;
 // Host integration must clear the explicit save destination on recovery.
 // Recovery is always dirty and never acknowledges an explicit user save.
}
