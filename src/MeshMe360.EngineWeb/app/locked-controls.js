// SPDX-License-Identifier: AGPL-3.0-only
export const toolbar = ['Select','Move','Rotate','Scale','Texture','Emboss','Deboss','Pattern','Repair','Hollow','Measure','Boolean','Split','Combine','STL','3MF','OBJ','Import'];
export const quickActions = ['Center Model','Repair Mesh','Make Solid','Hollow Model','Generate Texture','Prepare for Print'];
export const presets = ['Stone','Rock','Scales','Bricks','Wood','Metal','Fabric','Carbon','Leather','Concrete','Sand','Custom'];
export const inspectorTabs = ['Texture','Material','Properties'];
export const shelfTabs = ['Library','Presets','Recent','Favorites'];
export class CommandRegistry {
  constructor() { this.commands = new Map(); this.busy=false; }
  register(name, execute, available = () => true, requirement = '') { this.commands.set(name, { execute, available, requirement }); }
  state(name) { const c = this.commands.get(name);const blocked=this.busy&&!/^(mode:|view:|tab:|Pan$|Orbit$|Zoom$|Fit$)/.test(name);return c ? { implemented:true, enabled:!blocked&&!!c.available(), reason:blocked?'Finish or cancel the current operation':c.requirement } : {implemented:false, enabled:false, reason:'Implementation pending — milestone acceptance has not passed'}; }
  execute(name) { const c = this.commands.get(name),state=this.state(name); if (!state.enabled) throw new Error(state.reason); return c.execute(); }
  report(names) { return names.map(name => ({name,...this.state(name)})); }
}
