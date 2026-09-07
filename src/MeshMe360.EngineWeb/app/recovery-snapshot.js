// SPDX-License-Identifier: AGPL-3.0-only
import {serializeProject,deserializeProject} from './scene-document.js';
// M3 format contract for the M6 host coordinator. Timing, retention, disk paths
// and crash-session decisions belong to that coordinator, not this serializer.
export function createRecoverySnapshot(document,{processing=false}={}){
 if(processing||!document.dirty)return null;
 return serializeProject(document.state);
}
export function restoreRecoverySnapshot(document,bytes){
 // Parse and validate before changing any currently valid state.
 const recovered=deserializeProject(bytes);
 document.state=recovered;document.undoStack=[];document.redoStack=[];
 document.revision=document.nextRevision++;document.savedRevision=-1;
 // Host integration must clear the explicit save destination on recovery.
 // Recovery is always dirty and never acknowledges an explicit user save.
}
