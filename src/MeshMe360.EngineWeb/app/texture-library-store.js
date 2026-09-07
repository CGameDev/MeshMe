// SPDX-License-Identifier: AGPL-3.0-only
/** Persists custom source assets in the local texture library. */
// User-owned entries only. Built-in presets never enter this mutable store.
export class TextureLibraryStore {
 constructor(name='MeshMe360.TextureLibrary'){this.name=name;this.database=null;}
 async open(){if(this.database)return this.database;this.database=await new Promise((resolve,reject)=>{const request=indexedDB.open(this.name,1);request.onupgradeneeded=()=>request.result.createObjectStore('textures',{keyPath:'id'});request.onsuccess=()=>resolve(request.result);request.onerror=()=>reject(request.error);request.onblocked=()=>reject(new Error('Texture library is open in another application instance'));});return this.database;}
 async operation(mode,action){const db=await this.open();return new Promise((resolve,reject)=>{const transaction=db.transaction('textures',mode);let value;transaction.oncomplete=()=>resolve(value);transaction.onerror=()=>reject(transaction.error);transaction.onabort=()=>reject(transaction.error??new Error('Library transaction cancelled'));try{const request=action(transaction.objectStore('textures'));request.onsuccess=()=>{value=request.result;};}catch(error){transaction.abort();reject(error);}});}
 async list(){return(await this.operation('readonly',store=>store.getAll())).sort((a,b)=>a.name.localeCompare(b.name));}
 async put(item){if(!/^custom-[0-9a-f-]{36}$/.test(item.id)||!item.name?.trim()||!(item.blob instanceof Blob)||item.blob.size>32*1024*1024)throw new Error('Invalid custom texture (32 MiB maximum)');return this.operation('readwrite',store=>store.put(item));}
 async remove(id){if(!/^custom-[0-9a-f-]{36}$/.test(id))throw new Error('Built-in textures cannot be deleted');return this.operation('readwrite',store=>store.delete(id));}
 close(){this.database?.close();this.database=null;}
}
