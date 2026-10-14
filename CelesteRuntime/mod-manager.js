(() => {
  let dialog;
  window.celesteOpenModManager = async () => {
    if (!dialog) {
      dialog=document.createElement('dialog');
      dialog.style.cssText='width:min(680px,90vw);max-height:85vh;overflow:auto;background:#192239;color:white;padding:20px;border:1px solid #8998b9;border-radius:12px';
      dialog.innerHTML='<h2>Mod Manager</h2><p>Changes take effect after restarting the game.</p><p>Bundled: MobileBridge, MobileTweaks, MouseUI</p><label>Install mod ZIP <input type="file" accept=".zip"></label><p role="status"></p><div data-mods></div><button data-close>Close</button>';
      dialog.querySelector('[data-close]').onclick=()=>dialog.close();
      dialog.querySelector('input').onchange=async event=>{
        const file=event.target.files[0]; if(!file)return;
        const url=URL.createObjectURL(file);
        try {await window.celesteInstallModZip(url,file.name);status('Installed. Restart to load the mod.');await refresh();}
        catch(error){status(error.message);}finally{URL.revokeObjectURL(url);event.target.value='';}
      };
      document.body.appendChild(dialog);
    }
    if(!dialog.open)dialog.showModal();
    try{await refresh();}catch(error){status(error.message);}
  };
  function status(message){dialog.querySelector('[role=status]').textContent=message;}
  async function refresh(){
    await window.celesteBootMaintenance?.();
    const mods=await window.celesteListInstalledMods();
    const container=dialog.querySelector('[data-mods]');container.replaceChildren();
    for(const mod of mods){
      const row=document.createElement('p'), label=document.createElement('label'), toggle=document.createElement('input');
      toggle.type='checkbox';toggle.checked=mod.enabled;
      label.append(toggle,document.createTextNode(' '+mod.name));row.append(label);container.append(row);
      toggle.onchange=async()=>{try{await window.celesteSetModEnabled(mod.name,toggle.checked);status('Updated. Restart to apply.');await refresh();}catch(error){status(error.message);}};
    }
    if(!mods.length)container.textContent='No additional mods installed.';
  }
})();
