(() => {
  'use strict';
  const keys = { ArrowUp: [38, 'ArrowUp'], ArrowDown: [40, 'ArrowDown'], ArrowLeft: [37, 'ArrowLeft'], ArrowRight: [39, 'ArrowRight'], KeyC: [67, 'c'], KeyX: [88, 'x'], KeyZ: [90, 'z'], KeyV: [86, 'v'], Escape: [27, 'Escape'] };
  const held = new Map();
  function emit(code, down) {
    const [keyCode, key] = keys[code];
    const target = document.querySelector('canvas') || document;
    target.dispatchEvent(new KeyboardEvent(down ? 'keydown' : 'keyup', { code, key, keyCode, which: keyCode, bubbles: true, cancelable: true }));
  }
  function hold(id, codes) {
    const before = new Set([...held.values()].flat());
    if (codes.length) held.set(id, codes); else held.delete(id);
    const after = new Set([...held.values()].flat());
    for (const code of before) if (!after.has(code)) emit(code, false);
    for (const code of after) if (!before.has(code)) emit(code, true);
  }
  function release() { for (const id of [...held.keys()]) hold(id, []); }
  const read = (key, fallback) => { try { return JSON.parse(localStorage.getItem(key)) ?? fallback; } catch { return fallback; } };
  const save = (key, value) => { try { localStorage.setItem(key, JSON.stringify(value)); } catch {} };
  const root = document.createElement('div');
  root.id = 'celeste-touch-controls';
  root.innerHTML = `<style>
    #celeste-touch-controls {position:fixed;inset:0;z-index:2147483647;pointer-events:none;display:none;touch-action:none;user-select:none}
    body.game-rendered #celeste-touch-controls.visible {display:block}
    #celeste-touch-controls [hidden] {display:none!important}
    #celeste-touch-controls button {position:absolute;pointer-events:auto;touch-action:none;border:2px solid #ffffff80;border-radius:50%;background:#18233b66;color:white;font:600 14px sans-serif;padding:0;transform:translate(-50%,-50%);box-sizing:border-box}
    #celeste-touch-controls button.pressed {background:#bac9ed99}
    #celeste-touch-controls .stick-knob {position:absolute;left:50%;top:50%;width:36%;height:36%;border-radius:50%;background:#e4edffb0;transform:translate(-50%,-50%);pointer-events:none}
    #celeste-touch-controls [data-control=stick] {background:repeating-conic-gradient(#ffffff30 0deg 1deg,transparent 1deg 45deg),#18233b66}
    #celeste-touch-controls .touch-tools {left:50%;top:6%;width:76px;height:30px;border-radius:10px;font-size:12px}
    #celeste-touch-controls.editing button {border-color:#ffe474;background:#444b6bbb}
    #celeste-touch-controls .touch-panel {position:absolute;pointer-events:auto;left:50%;top:15%;transform:translateX(-50%);background:#151c30ee;color:white;padding:12px;border-radius:8px;font:14px sans-serif;display:none;max-width:80vw}
    #celeste-touch-controls.editing .touch-panel {display:block}
    #celeste-touch-controls .touch-panel label {display:inline-block;margin:5px}
    #celeste-touch-controls .touch-panel button {position:static;transform:none;border-radius:5px;padding:6px;margin:5px}
  </style>`;
  let editing = false;
  let gameplay = false;
  let pauseOnly = false;
  let alwaysOn = read('celeste.option.controls_always_on', false) === true;
  let enabled = read('celeste.option.touch_controls', true) !== false;
  let keyboardControls = read('celeste.option.keyboard_controls', false) === true;
  let joystick = read('celeste.option.joystick_mode', true) !== false;
  let snap = read('celeste.option.joystick_snap_8way', true) !== false;
  let layout = read('celeste.touch.layout.v1', {});
  const controls = [];
  const defaults = [
    ['stick', 'Move', null, 15, 72, 126],
    ['up', '▲', 'ArrowUp', 15, 57, 52], ['down', '▼', 'ArrowDown', 15, 84, 52],
    ['left', '◀', 'ArrowLeft', 8, 71, 52], ['right', '▶', 'ArrowRight', 22, 71, 52],
    ['jump', 'Jump', 'KeyC', 88, 77, 72], ['dash', 'Dash', 'KeyX', 78, 62, 66],
    ['grab', 'Grab', 'KeyZ', 91, 43, 64], ['pause', 'Ⅱ', 'Escape', 94, 10, 42],
    ['crouch-dash', 'Crouch dash', 'KeyV', 76, 84, 66]
  ];
  function refresh() {
    root.classList.toggle('visible', !keyboardControls && (editing || gameplay || pauseOnly || alwaysOn));
    for (const c of controls) {
      const p = layout[c.id] || { x:c.x, y:c.y, size:c.size };
      c.button.style.left = `${p.x}%`; c.button.style.top = `${p.y}%`;
      c.button.style.width = c.button.style.height = `${p.size}px`;
      c.button.hidden = keyboardControls || !enabled || (!editing && !alwaysOn && pauseOnly && c.id !== 'pause') || (c.id === 'stick' ? !joystick : ['up','down','left','right'].includes(c.id) && joystick);
    }
  }
  function edit(value) { release(); editing = value; root.classList.toggle('editing', value); refresh(); }
  for (const [id,label,code,x,y,size] of defaults) {
    const button = document.createElement('button');
    button.type = 'button'; button.textContent = label; button.setAttribute('aria-label', label); button.dataset.control = id;
    let knob;
    if(id==='stick') { button.textContent=''; knob=document.createElement('span'); knob.className='stick-knob'; button.appendChild(knob); }
    root.appendChild(button);
    const c = {id,x,y,size,button}; controls.push(c);
    let active = null, start = null;
    function move(event) {
      if (active !== event.pointerId) return;
      event.preventDefault(); event.stopPropagation();
      if (editing) {
        layout[id] = { x: Math.max(3, Math.min(97, start.x + (event.clientX-start.px)/innerWidth*100)), y: Math.max(5, Math.min(95, start.y + (event.clientY-start.py)/innerHeight*100)), size:start.size };
        refresh(); return;
      }
      if (code) { hold(active, [code]); return; }
      const rect = button.getBoundingClientRect();
      let dx = (event.clientX - rect.left - rect.width/2)/(rect.width/2), dy = (event.clientY-rect.top-rect.height/2)/(rect.height/2);
      if (Math.hypot(dx,dy) < .23) { hold(active, []); knob.style.left=knob.style.top='50%'; return; }
      if (snap) { const angle = Math.round(Math.atan2(dy,dx)/(Math.PI/4))*Math.PI/4; dx=Math.cos(angle); dy=Math.sin(angle); }
      const magnitude=Math.max(1,Math.hypot(dx,dy));
      knob.style.left=`${50+dx/magnitude*32}%`; knob.style.top=`${50+dy/magnitude*32}%`;
      hold(active, [...(dx < -.35 ? ['ArrowLeft'] : dx > .35 ? ['ArrowRight'] : []), ...(dy < -.35 ? ['ArrowUp'] : dy > .35 ? ['ArrowDown'] : [])]);
    }
    c.down = event => {
      event.preventDefault(); event.stopPropagation(); if (active !== null) return;
      active = event.pointerId; const p = layout[id] || c; start = {...p,px:event.clientX,py:event.clientY};
      try {button.setPointerCapture(active);} catch {}
      button.classList.add('pressed'); move(event);
    };
    button.addEventListener('pointerdown', c.down);
    button.addEventListener('pointermove', move);
    const up = event => { event.stopPropagation(); if (active !== event.pointerId) return; hold(active, []); active=null; button.classList.remove('pressed'); if(knob)knob.style.left=knob.style.top='50%'; if(editing) save('celeste.touch.layout.v1',layout); };
    for (const type of ['pointerup','pointercancel','lostpointercapture']) button.addEventListener(type,up);
    button.addEventListener('click', event => {event.preventDefault();event.stopPropagation();});
  }
  const tools = document.createElement('button'); tools.className='touch-tools'; tools.textContent='Controls'; tools.hidden=true; tools.onclick=()=>edit(!editing); root.appendChild(tools);
  const panel=document.createElement('div'); panel.className='touch-panel';
  panel.innerHTML='<div>Drag controls to move them.</div><label><input type="checkbox" data-option="enabled"> Show controls</label><label><input type="checkbox" data-option="always"> Always on (including menus)</label><label><input type="checkbox" data-option="joystick"> Joystick</label><label><input type="checkbox" data-option="snap"> 8-way snap</label><label>Size <input type="range" min="35" max="160" value="72" aria-label="Control size"></label><button type="button" data-reset>Reset layout</button><button type="button" data-done>Done</button>';
  root.appendChild(panel);
  const options=[['enabled','touch_controls'],['always','controls_always_on'],['joystick','joystick_mode'],['snap','joystick_snap_8way']];
  function setOption(key,value) {
    release();
    if(key==='pause_only')pauseOnly=value;
    if(key==='touch_controls')enabled=value;
    if(key==='keyboard_controls') { keyboardControls=value; if(value && editing)edit(false); }
    if(key==='controls_always_on')alwaysOn=value;
    if(key==='joystick_mode')joystick=value;
    if(key==='joystick_snap_8way')snap=value;
    save('celeste.option.'+key,value);
    const option=options.find(item=>item[1]===key);
    if(option)panel.querySelector(`[data-option="${option[0]}"]`).checked=value;
    refresh();
  }
  for (const [name,key] of options) {
    const value={enabled,always:alwaysOn,joystick,snap}[name];
    const input=panel.querySelector(`[data-option="${name}"]`); input.checked=value;
    input.onchange=()=> {setOption(key,input.checked); window.celesteQueueControlOption?.(key,input.checked);};
  }
  panel.querySelector('input[type=range]').oninput=event=> { for(const c of controls)layout[c.id]={...(layout[c.id]||{x:c.x,y:c.y}),size:Number(event.target.value)*(c.id==='stick'?1.6:1)}; save('celeste.touch.layout.v1',layout);refresh(); };
  panel.querySelector('[data-reset]').onclick=()=>{release();layout={};save('celeste.touch.layout.v1',layout);panel.querySelector('input[type=range]').value='72';refresh();};
  panel.querySelector('[data-done]').onclick=()=>edit(false);
  for(const type of ['pointerdown','pointerup','touchstart','touchend']) panel.addEventListener(type,event=>event.stopPropagation());
  addEventListener('celeste-open-layout-editor',()=>edit(true));
  addEventListener('celeste-option-changed',event=>setOption(event.detail.key,String(event.detail.value).toLowerCase()==='true'));
  window.celesteAndroidSetGameplay=value=>{if(gameplay!==!!value)release();gameplay=!!value;refresh();console.info('[android-port] gameplay controls state: '+gameplay);};
  document.addEventListener('pointerdown',event=>{
    if(editing || keyboardControls || !enabled || !gameplay || event.target.closest('#celeste-touch-controls,dialog'))return;
    let nearest=null, best=Infinity;
    for(const c of controls) {
      if(c.button.hidden || c.id==='pause')continue;
      const r=c.button.getBoundingClientRect(), distance=Math.hypot(event.clientX-r.left-r.width/2,event.clientY-r.top-r.height/2)-r.width/2;
      if(distance<best && distance<48){nearest=c;best=distance;}
    }
    if(nearest){event.stopImmediatePropagation();nearest.down(event);}
  },true);
  addEventListener('blur',release); document.addEventListener('visibilitychange',()=>{if(document.hidden)release();});
  document.body.appendChild(root); refresh();
  console.info('[android-port] touch controls installed');
})();
