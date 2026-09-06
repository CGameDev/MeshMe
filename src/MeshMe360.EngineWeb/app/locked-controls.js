// SPDX-License-Identifier: AGPL-3.0-only
export const toolbar = ['Select','Move','Rotate','Scale','Texture','Emboss','Deboss','Pattern','Repair','Hollow','Measure','Boolean','Split','Combine','STL','3MF','OBJ','Import'];
export const quickActions = ['Center Model','Repair Mesh','Make Solid','Hollow Model','Generate Texture','Prepare for Print'];
export const presets = ['Stone','Rock','Scales','Bricks','Wood','Metal','Fabric','Carbon','Leather','Concrete','Sand','Custom'];
export const inspectorTabs = ['Texture','Material','Properties'];
export const shelfTabs = ['Library','Presets','Recent','Favorites'];
export class CommandRegistry {
  constructor() { this.commands = new Map(); }
  register(name, execute, available = () => true, requirement = '') { this.commands.set(name, { execute, available, requirement }); }
  state(name) { const c = this.commands.get(name); return c ? { implemented:true, enabled:!!c.available(), reason:c.requirement } : {implemented:false, enabled:false, reason:'Implementation pending — milestone acceptance has not passed'}; }
  execute(name) { const c = this.commands.get(name); if (!c || !c.available()) throw new Error(this.state(name).reason); return c.execute(); }
  report(names) { return names.map(name => ({name,...this.state(name)})); }
}
