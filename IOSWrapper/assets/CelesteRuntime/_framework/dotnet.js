//! Licensed to the .NET Foundation under one or more agreements.
//! The .NET Foundation licenses this file to you under the MIT license.

var e=!0;const t=async()=>WebAssembly.validate(new Uint8Array([0,97,115,109,1,0,0,0,1,4,1,96,0,0,3,2,1,0,10,8,1,6,0,6,64,25,11,11])),o=async()=>WebAssembly.validate(new Uint8Array([0,97,115,109,1,0,0,0,1,5,1,96,0,1,123,3,2,1,0,10,15,1,13,0,65,1,253,15,65,2,253,15,253,128,2,11])),n=async()=>WebAssembly.validate(new Uint8Array([0,97,115,109,1,0,0,0,1,5,1,96,0,1,123,3,2,1,0,10,10,1,8,0,65,0,253,15,253,98,11])),r=Symbol.for("wasm promise_control");function i(e,t){let o=null;const n=new Promise((function(n,r){o={isDone:!1,promise:null,resolve:t=>{o.isDone||(o.isDone=!0,n(t),e&&e())},reject:e=>{o.isDone||(o.isDone=!0,r(e),t&&t())}}}));o.promise=n;const i=n;return i[r]=o,{promise:i,promise_control:o}}function s(e){return e[r]}function a(e){e&&function(e){return void 0!==e[r]}(e)||qe(!1,"Promise is not controllable")}const l="__mono_message__",c=["debug","log","trace","warn","info","error"],d="MONO_WASM: ";let u,f,m,g;function p(e){g=e}function h(e){if(Ne.diagnosticTracing){const t="function"==typeof e?e():e;console.debug(d+t)}}function w(e,...t){console.info(d+e,...t)}function b(e,...t){console.info(e,...t)}function y(e,...t){console.warn(d+e,...t)}function v(e,...t){if(t&&t.length>0&&t[0]&&"object"==typeof t[0]){if(t[0].silent)return;if(t[0].toString)return void console.error(d+e,t[0].toString())}console.error(d+e,...t)}let _,E,T="",x=(new Date).valueOf();function j(t,o,n){return function(...r){try{let i=r[0];if(void 0===i)i="undefined";else if(null===i)i="null";else if("function"==typeof i)i=i.toString();else if("string"!=typeof i)try{i=JSON.stringify(i)}catch(e){i=i.toString()}if("string"==typeof i&&e){if(Pe&&-1!==i.indexOf("keeping the worker alive for asynchronous operation"))return;if(0===i.indexOf("MONO_WASM: ")||0===i.indexOf("[MONO]")){const e=new Date;x!==e.valueOf()&&(T=e.toISOString().substring(11,23),x=e.valueOf()),i=`[${g} ${T}] ${i}`}}o(n?JSON.stringify({method:t,payload:i,arguments:r.slice(1)}):[t+i,...r.slice(1)])}catch(e){m.error(`proxyConsole failed: ${e}`)}}}function R(e,t,o){f=t,g=e,m={...t};const n=`${o}/console`.replace("https://","wss://").replace("http://","ws://");u=new WebSocket(n),u.addEventListener("error",O),u.addEventListener("close",D),function(){for(const e of c)f[e]=j(`console.${e}`,A,!0)}()}function S(e){let t=30;const o=()=>{u?0==u.bufferedAmount||0==t?(e&&b(e),function(){for(const e of c)f[e]=j(`console.${e}`,m.log,!1)}(),u.removeEventListener("error",O),u.removeEventListener("close",D),u.close(1e3,e),u=void 0):(t--,globalThis.setTimeout(o,100)):e&&m&&m.log(e)};o()}function A(e){u&&u.readyState===WebSocket.OPEN?u.send(e):m.log(e)}function O(e){m.error(`[${g}] proxy console websocket error: ${e}`,e)}function D(e){m.debug(`[${g}] proxy console websocket closed: ${e}`,e)}function k(){Ne.preferredIcuAsset=I(Ne.config);let e="invariant"==Ne.config.globalizationMode;if(!e)if(Ne.preferredIcuAsset)Ne.diagnosticTracing&&h("ICU data archive(s) available, disabling invariant mode");else{if("custom"===Ne.config.globalizationMode||"all"===Ne.config.globalizationMode||"sharded"===Ne.config.globalizationMode){const e="invariant globalization mode is inactive and no ICU data archives are available";throw v(`ERROR: ${e}`),new Error(e)}Ne.diagnosticTracing&&h("ICU data archive(s) not available, using invariant globalization mode"),e=!0,Ne.preferredIcuAsset=null}const t="DOTNET_SYSTEM_GLOBALIZATION_INVARIANT",o=Ne.config.environmentVariables;if(void 0===o[t]&&e&&(o[t]="1"),void 0===o.TZ)try{const e=Intl.DateTimeFormat().resolvedOptions().timeZone||null;e&&(o.TZ=e)}catch(e){w("failed to detect timezone, will fallback to UTC")}}function I(e){var t;if((null===(t=e.resources)||void 0===t?void 0:t.icu)&&"invariant"!=e.globalizationMode){const t=e.applicationCulture||(Ce?globalThis.navigator&&globalThis.navigator.languages&&globalThis.navigator.languages[0]:Intl.DateTimeFormat().resolvedOptions().locale),o=e.resources.icu;let n=null;if("custom"===e.globalizationMode){if(o.length>=1)return o[0].name}else t&&"all"!==e.globalizationMode?"sharded"===e.globalizationMode&&(n=function(e){const t=e.split("-")[0];return"en"===t||["fr","fr-FR","it","it-IT","de","de-DE","es","es-ES"].includes(e)?"icudt_EFIGS.dat":["zh","ko","ja"].includes(t)?"icudt_CJK.dat":"icudt_no_CJK.dat"}(t)):n="icudt.dat";if(n)for(let e=0;e<o.length;e++){const t=o[e];if(t.virtualPath===n)return t.name}}return e.globalizationMode="invariant",null}const P=class{constructor(e){this.url=e}toString(){return this.url}};async function C(e,t){try{const o="function"==typeof globalThis.fetch;if(De){const n=e.startsWith("file://");if(!n&&o)return globalThis.fetch(e,t||{credentials:"same-origin"});_||(E=ze.require("url"),_=ze.require("fs")),n&&(e=E.fileURLToPath(e));const r=await _.promises.readFile(e);return{ok:!0,headers:{length:0,get:()=>null},url:e,arrayBuffer:()=>r,json:()=>JSON.parse(r),text:()=>{throw new Error("NotImplementedException")}}}if(o)return globalThis.fetch(e,t||{credentials:"same-origin"});if("function"==typeof read)return{ok:!0,url:e,headers:{length:0,get:()=>null},arrayBuffer:()=>new Uint8Array(read(e,"binary")),json:()=>JSON.parse(read(e,"utf8")),text:()=>read(e,"utf8")}}catch(t){return{ok:!1,url:e,status:500,headers:{length:0,get:()=>null},statusText:"ERR28: "+t,arrayBuffer:()=>{throw t},json:()=>{throw t},text:()=>{throw t}}}throw new Error("No fetch implementation available")}function M(e){return"string"!=typeof e&&qe(!1,"url must be a string"),!L(e)&&0!==e.indexOf("./")&&0!==e.indexOf("../")&&globalThis.URL&&globalThis.document&&globalThis.document.baseURI&&(e=new URL(e,globalThis.document.baseURI).toString()),e}const U=/^[a-zA-Z][a-zA-Z\d+\-.]*?:\/\//,N=/[a-zA-Z]:[\\/]/;function L(e){return De||Me?e.startsWith("/")||e.startsWith("\\")||-1!==e.indexOf("///")||N.test(e):U.test(e)}let $,z=0;const W=[],F=[],B=new Map,V={"js-module-threads":!0,"js-module-runtime":!0,"js-module-dotnet":!0,"js-module-native":!0,"js-module-diagnostics":!0},q={...V,"js-module-library-initializer":!0},J={...V,dotnetwasm:!0,heap:!0,manifest:!0},H={...q,manifest:!0},Z={...q,dotnetwasm:!0},Q={dotnetwasm:!0,symbols:!0},G={...q,dotnetwasm:!0,symbols:!0},K={symbols:!0};function X(e){return!("icu"==e.behavior&&e.name!=Ne.preferredIcuAsset)}function Y(e,t,o){null!=t||(t=[]),qe(1==t.length,`Expect to have one ${o} asset in resources`);const n=t[0];return n.behavior=o,ee(n),e.push(n),n}function ee(e){J[e.behavior]&&B.set(e.behavior,e)}function te(e){qe(J[e],`Unknown single asset behavior ${e}`);const t=B.get(e);if(t&&!t.resolvedUrl)if(t.resolvedUrl=Ne.locateFile(t.name),V[t.behavior]){const e=he(t);e?("string"!=typeof e&&qe(!1,"loadBootResource response for 'dotnetjs' type should be a URL string"),t.resolvedUrl=e):t.resolvedUrl=ue(t.resolvedUrl,t.behavior)}else if("dotnetwasm"!==t.behavior)throw new Error(`Unknown single asset behavior ${e}`);return t}function oe(e){const t=te(e);return qe(t,`Single asset for ${e} not found`),t}let ne=!1;async function re(){if(!ne){ne=!0,Ne.diagnosticTracing&&h("mono_download_assets");try{const e=[],t=[],o=(e,t)=>{!G[e.behavior]&&X(e)&&Ne.expected_instantiated_assets_count++,!Z[e.behavior]&&X(e)&&(Ne.expected_downloaded_assets_count++,t.push(le(e)))};for(const t of W)o(t,e);for(const e of F)o(e,t);Ne.allDownloadsQueued.promise_control.resolve(),Promise.all([...e,...t]).then((()=>{Ne.allDownloadsFinished.promise_control.resolve()})).catch((e=>{throw Ne.err("Error in mono_download_assets: "+e),et(1,e),e})),await Ne.runtimeModuleLoaded.promise;const n=async e=>{const t=await e;if(t.buffer){if(!G[t.behavior]){t.buffer&&"object"==typeof t.buffer||qe(!1,"asset buffer must be array-like or buffer-like or promise of these"),"string"!=typeof t.resolvedUrl&&qe(!1,"resolvedUrl must be string");const e=t.resolvedUrl,o=await t.buffer,n=new Uint8Array(o);we(t),await Ue.beforeOnRuntimeInitialized.promise,Ue.instantiate_asset(t,e,n)}}else Q[t.behavior]?("symbols"===t.behavior&&(await Ue.instantiate_symbols_asset(t),we(t)),Q[t.behavior]&&++Ne.actual_downloaded_assets_count):(t.isOptional||qe(!1,"Expected asset to have the downloaded buffer"),!Z[t.behavior]&&X(t)&&Ne.expected_downloaded_assets_count--,!G[t.behavior]&&X(t)&&Ne.expected_instantiated_assets_count--)},r=[],i=[];for(const t of e)r.push(n(t));for(const e of t)i.push(n(e));Promise.all(r).then((()=>{Pe||Ue.coreAssetsInMemory.promise_control.resolve()})).catch((e=>{throw Ne.err("Error in mono_download_assets: "+e),et(1,e),e})),Promise.all(i).then((async()=>{Pe||(await Ue.coreAssetsInMemory.promise,Ue.allAssetsInMemory.promise_control.resolve())})).catch((e=>{throw Ne.err("Error in mono_download_assets: "+e),et(1,e),e}))}catch(e){throw Ne.err("Error in mono_download_assets: "+e),e}}}let ie=!1;function se(){if(ie)return;ie=!0;const e=Ne.config,t=[];if(e.assets)for(const t of e.assets)"object"!=typeof t&&qe(!1,`asset must be object, it was ${typeof t} : ${t}`),"string"!=typeof t.behavior&&qe(!1,"asset behavior must be known string"),"string"!=typeof t.name&&qe(!1,"asset name must be string"),t.resolvedUrl&&"string"!=typeof t.resolvedUrl&&qe(!1,"asset resolvedUrl could be string"),t.hash&&"string"!=typeof t.hash&&qe(!1,"asset resolvedUrl could be string"),t.pendingDownload&&"object"!=typeof t.pendingDownload&&qe(!1,"asset pendingDownload could be object"),t.isCore?W.push(t):F.push(t),ee(t);else if(e.resources){const o=e.resources;o.wasmNative||qe(!1,"resources.wasmNative must be defined"),o.jsModuleNative||qe(!1,"resources.jsModuleNative must be defined"),o.jsModuleRuntime||qe(!1,"resources.jsModuleRuntime must be defined"),o.jsModuleWorker||qe(!1,"resources.jsModuleWorker must be defined"),Y(F,o.wasmNative,"dotnetwasm"),Y(t,o.jsModuleNative,"js-module-native"),Y(t,o.jsModuleRuntime,"js-module-runtime"),o.jsModuleDiagnostics&&Y(t,o.jsModuleDiagnostics,"js-module-diagnostics"),Y(t,o.jsModuleWorker,"js-module-threads");const n=(e,t,o)=>{const n=e;n.behavior=t,o?(n.isCore=!0,W.push(n)):F.push(n)};if(o.coreAssembly)for(let e=0;e<o.coreAssembly.length;e++)n(o.coreAssembly[e],"assembly",!0);if(o.assembly)for(let e=0;e<o.assembly.length;e++)n(o.assembly[e],"assembly",!o.coreAssembly);if(0!=e.debugLevel&&Ne.isDebuggingSupported()){if(o.corePdb)for(let e=0;e<o.corePdb.length;e++)n(o.corePdb[e],"pdb",!0);if(o.pdb)for(let e=0;e<o.pdb.length;e++)n(o.pdb[e],"pdb",!o.corePdb)}if(e.loadAllSatelliteResources&&o.satelliteResources)for(const e in o.satelliteResources)for(let t=0;t<o.satelliteResources[e].length;t++){const r=o.satelliteResources[e][t];r.culture=e,n(r,"resource",!o.coreAssembly)}if(o.coreVfs)for(let e=0;e<o.coreVfs.length;e++)n(o.coreVfs[e],"vfs",!0);if(o.vfs)for(let e=0;e<o.vfs.length;e++)n(o.vfs[e],"vfs",!o.coreVfs);const r=I(e);if(r&&o.icu)for(let e=0;e<o.icu.length;e++){const t=o.icu[e];t.name===r&&n(t,"icu",!1)}if(o.wasmSymbols)for(let e=0;e<o.wasmSymbols.length;e++)n(o.wasmSymbols[e],"symbols",!1)}if(e.appsettings)for(let t=0;t<e.appsettings.length;t++){const o=e.appsettings[t],n=be(o);"appsettings.json"!==n&&n!==`appsettings.${e.applicationEnvironment}.json`||F.push({name:o,behavior:"vfs",cache:"no-cache",useCredentials:!0})}e.assets=[...W,...F,...t]}async function ae(e){const t=await le(e);return await t.pendingDownloadInternal.response,t.buffer}async function le(e){try{return await ce(e)}catch(t){if(!Ne.enableDownloadRetry)throw t;if(Me||De)throw t;if(e.pendingDownload&&e.pendingDownloadInternal==e.pendingDownload)throw t;if(e.resolvedUrl&&-1!=e.resolvedUrl.indexOf("file://"))throw t;if(t&&404==t.status)throw t;e.pendingDownloadInternal=void 0,await Ne.allDownloadsQueued.promise;try{return Ne.diagnosticTracing&&h(`Retrying download '${e.name}'`),await ce(e)}catch(t){return e.pendingDownloadInternal=void 0,await new Promise((e=>globalThis.setTimeout(e,100))),Ne.diagnosticTracing&&h(`Retrying download (2) '${e.name}' after delay`),await ce(e)}}}async function ce(e){for(;$;)await $.promise;try{++z,z==Ne.maxParallelDownloads&&(Ne.diagnosticTracing&&h("Throttling further parallel downloads"),$=i());const t=await async function(e){if(e.pendingDownload&&(e.pendingDownloadInternal=e.pendingDownload),e.pendingDownloadInternal&&e.pendingDownloadInternal.response)return e.pendingDownloadInternal.response;if(e.buffer){const t=await e.buffer;return e.resolvedUrl||(e.resolvedUrl="undefined://"+e.name),e.pendingDownloadInternal={url:e.resolvedUrl,name:e.name,response:Promise.resolve({ok:!0,arrayBuffer:()=>t,json:()=>JSON.parse(new TextDecoder("utf-8").decode(t)),text:()=>{throw new Error("NotImplementedException")},headers:{get:()=>{}}})},e.pendingDownloadInternal.response}const t=e.loadRemote&&Ne.config.remoteSources?Ne.config.remoteSources:[""];let o;for(let n of t){n=n.trim(),"./"===n&&(n="");const t=de(e,n);e.name===t?Ne.diagnosticTracing&&h(`Attempting to download '${t}'`):Ne.diagnosticTracing&&h(`Attempting to download '${t}' for ${e.name}`);try{e.resolvedUrl=t;const n=ge(e);if(e.pendingDownloadInternal=n,o=await n.response,!o||!o.ok)continue;return o}catch(e){o||(o={ok:!1,url:t,status:0,statusText:""+e});continue}}const n=e.isOptional||e.name.match(/\.pdb$/)&&Ne.config.ignorePdbLoadErrors;if(o||qe(!1,`Response undefined ${e.name}`),!n){const t=new Error(`download '${o.url}' for ${e.name} failed ${o.status} ${o.statusText}`);throw t.status=o.status,t}w(`optional download '${o.url}' for ${e.name} failed ${o.status} ${o.statusText}`)}(e);return t?(Q[e.behavior]||(e.buffer=await t.arrayBuffer(),++Ne.actual_downloaded_assets_count),e):e}finally{if(--z,$&&z==Ne.maxParallelDownloads-1){Ne.diagnosticTracing&&h("Resuming more parallel downloads");const e=$;$=void 0,e.promise_control.resolve()}}}function de(e,t){let o;return null==t&&qe(!1,`sourcePrefix must be provided for ${e.name}`),e.resolvedUrl?o=e.resolvedUrl:(o=""===t?"assembly"===e.behavior||"pdb"===e.behavior?e.name:"resource"===e.behavior&&e.culture&&""!==e.culture?`${e.culture}/${e.name}`:e.name:t+e.name,o=ue(Ne.locateFile(o),e.behavior)),o&&"string"==typeof o||qe(!1,"attemptUrl need to be path or url string"),o}function ue(e,t){return Ne.modulesUniqueQuery&&H[t]&&(e+=Ne.modulesUniqueQuery),e}let fe=0;const me=new Set;function ge(e){try{e.resolvedUrl||qe(!1,"Request's resolvedUrl must be set");const t=function(e){let t=e.resolvedUrl;if(Ne.loadBootResource){const o=he(e);if(o instanceof Promise)return o;"string"==typeof o&&(t=o)}const o={};return e.cache?o.cache=e.cache:Ne.config.disableNoCacheFetch||(o.cache="no-cache"),e.useCredentials?o.credentials="include":!Ne.config.disableIntegrityCheck&&e.hash&&(o.integrity=e.hash),Ne.fetch_like(t,o)}(e),o={name:e.name,url:e.resolvedUrl,response:t};return me.add(e.name),o.response.then((()=>{"assembly"==e.behavior&&Ne.loadedAssemblies.push(e.name),fe++,Ne.onDownloadResourceProgress&&Ne.onDownloadResourceProgress(fe,me.size)})),o}catch(t){const o={ok:!1,url:e.resolvedUrl,status:500,statusText:"ERR29: "+t,arrayBuffer:()=>{throw t},json:()=>{throw t}};return{name:e.name,url:e.resolvedUrl,response:Promise.resolve(o)}}}const pe={resource:"assembly",assembly:"assembly",pdb:"pdb",icu:"globalization",vfs:"configuration",manifest:"manifest",dotnetwasm:"dotnetwasm","js-module-dotnet":"dotnetjs","js-module-native":"dotnetjs","js-module-runtime":"dotnetjs","js-module-threads":"dotnetjs"};function he(e){var t;if(Ne.loadBootResource){const o=null!==(t=e.hash)&&void 0!==t?t:"",n=e.resolvedUrl,r=pe[e.behavior];if(r){const t=Ne.loadBootResource(r,e.name,n,o,e.behavior);return"string"==typeof t?M(t):t}}}function we(e){e.pendingDownloadInternal=null,e.pendingDownload=null,e.buffer=null,e.moduleExports=null}function be(e){let t=e.lastIndexOf("/");return t>=0&&t++,e.substring(t)}async function ye(e){e&&await Promise.all((null!=e?e:[]).map((e=>async function(e){try{const t=e.name;if(!e.moduleExports){const o=ue(Ne.locateFile(t),"js-module-library-initializer");Ne.diagnosticTracing&&h(`Attempting to import '${o}' for ${e}`),e.moduleExports=await import(/*! webpackIgnore: true */o)}Ne.libraryInitializers.push({scriptName:t,exports:e.moduleExports})}catch(t){y(`Failed to import library initializer '${e}': ${t}`)}}(e))))}async function ve(e,t){if(!Ne.libraryInitializers)return;const o=[];for(let n=0;n<Ne.libraryInitializers.length;n++){const r=Ne.libraryInitializers[n];r.exports[e]&&o.push(_e(r.scriptName,e,(()=>r.exports[e](...t))))}await Promise.all(o)}async function _e(e,t,o){try{await o()}catch(o){throw y(`Failed to invoke '${t}' on library initializer '${e}': ${o}`),et(1,o),o}}function Ee(e,t){if(e===t)return e;const o={...t};return void 0!==o.assets&&o.assets!==e.assets&&(o.assets=[...e.assets||[],...o.assets||[]]),void 0!==o.resources&&(o.resources=xe(e.resources||{assembly:[],jsModuleNative:[],jsModuleRuntime:[],wasmNative:[]},o.resources)),void 0!==o.environmentVariables&&(o.environmentVariables={...e.environmentVariables||{},...o.environmentVariables||{}}),void 0!==o.runtimeOptions&&o.runtimeOptions!==e.runtimeOptions&&(o.runtimeOptions=[...e.runtimeOptions||[],...o.runtimeOptions||[]]),Object.assign(e,o)}function Te(e,t){if(e===t)return e;const o={...t};return o.config&&(e.config||(e.config={}),o.config=Ee(e.config,o.config)),Object.assign(e,o)}function xe(e,t){if(e===t)return e;const o={...t};return void 0!==o.coreAssembly&&(o.coreAssembly=[...e.coreAssembly||[],...o.coreAssembly||[]]),void 0!==o.assembly&&(o.assembly=[...e.assembly||[],...o.assembly||[]]),void 0!==o.lazyAssembly&&(o.lazyAssembly=[...e.lazyAssembly||[],...o.lazyAssembly||[]]),void 0!==o.corePdb&&(o.corePdb=[...e.corePdb||[],...o.corePdb||[]]),void 0!==o.pdb&&(o.pdb=[...e.pdb||[],...o.pdb||[]]),void 0!==o.jsModuleWorker&&(o.jsModuleWorker=[...e.jsModuleWorker||[],...o.jsModuleWorker||[]]),void 0!==o.jsModuleNative&&(o.jsModuleNative=[...e.jsModuleNative||[],...o.jsModuleNative||[]]),void 0!==o.jsModuleDiagnostics&&(o.jsModuleDiagnostics=[...e.jsModuleDiagnostics||[],...o.jsModuleDiagnostics||[]]),void 0!==o.jsModuleRuntime&&(o.jsModuleRuntime=[...e.jsModuleRuntime||[],...o.jsModuleRuntime||[]]),void 0!==o.wasmSymbols&&(o.wasmSymbols=[...e.wasmSymbols||[],...o.wasmSymbols||[]]),void 0!==o.wasmNative&&(o.wasmNative=[...e.wasmNative||[],...o.wasmNative||[]]),void 0!==o.icu&&(o.icu=[...e.icu||[],...o.icu||[]]),void 0!==o.satelliteResources&&(o.satelliteResources=function(e,t){if(e===t)return e;for(const o in t)e[o]=[...e[o]||[],...t[o]||[]];return e}(e.satelliteResources||{},o.satelliteResources||{})),void 0!==o.modulesAfterConfigLoaded&&(o.modulesAfterConfigLoaded=[...e.modulesAfterConfigLoaded||[],...o.modulesAfterConfigLoaded||[]]),void 0!==o.modulesAfterRuntimeReady&&(o.modulesAfterRuntimeReady=[...e.modulesAfterRuntimeReady||[],...o.modulesAfterRuntimeReady||[]]),void 0!==o.extensions&&(o.extensions={...e.extensions||{},...o.extensions||{}}),void 0!==o.vfs&&(o.vfs=[...e.vfs||[],...o.vfs||[]]),Object.assign(e,o)}function je(){const e=Ne.config;if(e.environmentVariables=e.environmentVariables||{},e.runtimeOptions=e.runtimeOptions||[],e.resources=e.resources||{assembly:[],jsModuleNative:[],jsModuleWorker:[],jsModuleRuntime:[],wasmNative:[],vfs:[],satelliteResources:{}},e.assets){Ne.diagnosticTracing&&h("config.assets is deprecated, use config.resources instead");for(const t of e.assets){const o={};switch(t.behavior){case"assembly":o.assembly=[t];break;case"pdb":o.pdb=[t];break;case"resource":o.satelliteResources={},o.satelliteResources[t.culture]=[t];break;case"icu":o.icu=[t];break;case"symbols":o.wasmSymbols=[t];break;case"vfs":o.vfs=[t];break;case"dotnetwasm":o.wasmNative=[t];break;case"js-module-threads":o.jsModuleWorker=[t];break;case"js-module-runtime":o.jsModuleRuntime=[t];break;case"js-module-native":o.jsModuleNative=[t];break;case"js-module-diagnostics":o.jsModuleDiagnostics=[t];break;case"js-module-dotnet":break;default:throw new Error(`Unexpected behavior ${t.behavior} of asset ${t.name}`)}xe(e.resources,o)}}e.debugLevel,e.applicationEnvironment||(e.applicationEnvironment="Production"),Number.isInteger(e.pthreadPoolInitialSize)||(e.pthreadPoolInitialSize=5),Number.isInteger(e.pthreadPoolUnusedSize)||(e.pthreadPoolUnusedSize=1),null==e.jsThreadBlockingMode&&(e.jsThreadBlockingMode="PreventSynchronousJSExport"),void 0===e.environmentVariables.MONO_SLEEP_ABORT_LIMIT&&(e.environmentVariables.MONO_SLEEP_ABORT_LIMIT="5000"),e.applicationCulture&&(e.environmentVariables.LANG=`${e.applicationCulture}.UTF-8`),Ue.diagnosticTracing=Ne.diagnosticTracing=!!e.diagnosticTracing,Ue.waitForDebugger=e.waitForDebugger,Ne.maxParallelDownloads=e.maxParallelDownloads||Ne.maxParallelDownloads,Ne.enableDownloadRetry=void 0!==e.enableDownloadRetry?e.enableDownloadRetry:Ne.enableDownloadRetry}let Re=!1;async function Se(e){var t;if(Re)return void await Ne.afterConfigLoaded.promise;let o;try{if(e.configSrc||Ne.config&&0!==Object.keys(Ne.config).length&&(Ne.config.assets||Ne.config.resources)||(e.configSrc="dotnet.boot.js"),o=e.configSrc,Re=!0,o&&(Ne.diagnosticTracing&&h("mono_wasm_load_config"),await async function(e){const t=e.configSrc,o=Ne.locateFile(t);let n=null;void 0!==Ne.loadBootResource&&(n=Ne.loadBootResource("manifest",t,o,"","manifest"));let r,i=null;if(n)if("string"==typeof n)n.includes(".json")?(i=await s(M(n)),r=await Oe(i)):r=(await import(M(n))).config;else{const e=await n;"function"==typeof e.json?(i=e,r=await Oe(i)):r=e.config}else o.includes(".json")?(i=await s(ue(o,"manifest")),r=await Oe(i)):r=(await import(ue(o,"manifest"))).config;function s(e){return Ne.fetch_like(e,{method:"GET",credentials:"include",cache:"no-cache"})}Ne.config.applicationEnvironment&&(r.applicationEnvironment=Ne.config.applicationEnvironment),Ee(Ne.config,r)}(e)),je(),await ye(null===(t=Ne.config.resources)||void 0===t?void 0:t.modulesAfterConfigLoaded),await ve("onRuntimeConfigLoaded",[Ne.config]),e.onConfigLoaded)try{await e.onConfigLoaded(Ne.config,$e),je()}catch(e){throw v("onConfigLoaded() failed",e),e}je(),Ne.afterConfigLoaded.promise_control.resolve(Ne.config)}catch(t){const n=`Failed to load config file ${o} ${t} ${null==t?void 0:t.stack}`;throw Ne.config=e.config=Object.assign(Ne.config,{message:n,error:t,isError:!0}),et(1,new Error(n)),t}}function Ae(){return!!globalThis.navigator&&(Ne.isChromium||Ne.isFirefox)}async function Oe(e){const t=Ne.config,o=await e.json();t.applicationEnvironment||o.applicationEnvironment||(o.applicationEnvironment=e.headers.get("Blazor-Environment")||e.headers.get("DotNet-Environment")||void 0),o.environmentVariables||(o.environmentVariables={});const n=e.headers.get("DOTNET-MODIFIABLE-ASSEMBLIES");n&&(o.environmentVariables.DOTNET_MODIFIABLE_ASSEMBLIES=n);const r=e.headers.get("ASPNETCORE-BROWSER-TOOLS");return r&&(o.environmentVariables.__ASPNETCORE_BROWSER_TOOLS=r),o}"function"!=typeof importScripts||globalThis.onmessage||(globalThis.dotnetSidecar=!0);const De="object"==typeof process&&"object"==typeof process.versions&&"string"==typeof process.versions.node,ke="function"==typeof importScripts,Ie=ke&&"undefined"!=typeof dotnetSidecar,Pe=ke&&!Ie,Ce="object"==typeof window||ke&&!De,Me=!Ce&&!De;let Ue={},Ne={},Le={},$e={},ze={},We=!1;const Fe={},Be={config:Fe},Ve={mono:{},binding:{},internal:ze,module:Be,loaderHelpers:Ne,runtimeHelpers:Ue,diagnosticHelpers:Le,api:$e};function qe(e,t){if(e)return;const o="Assert failed: "+("function"==typeof t?t():t),n=new Error(o);v(o,n),Ue.nativeAbort(n)}function Je(){return void 0!==Ne.exitCode}function He(){return Ue.runtimeReady&&!Je()}function Ze(){Je()&&qe(!1,`.NET runtime already exited with ${Ne.exitCode} ${Ne.exitReason}. You can use runtime.runMain() which doesn't exit the runtime.`),Pe?Ue.runtimeReady||qe(!1,"The WebWorker is not attached to the runtime. See https://github.com/dotnet/runtime/blob/main/src/mono/wasm/threads.md#JS-interop-on-dedicated-threads"):Ue.runtimeReady||qe(!1,".NET runtime didn't start yet. Please call dotnet.create() first.")}function Qe(){Ce&&(globalThis.addEventListener("unhandledrejection",ot),globalThis.addEventListener("error",nt))}let Ge,Ke;function Xe(e){Ke&&Ke(e),et(e,Ne.exitReason)}function Ye(e){var t;if(Ge&&Ge(e||Ne.exitReason),(null===(t=Ne.config)||void 0===t?void 0:t.dumpThreadsOnNonZeroExit)&&Ue.mono_wasm_print_thread_dump&&void 0===Ne.exitCode)try{Ue.mono_wasm_print_thread_dump()}catch(e){}et(1,e||Ne.exitReason)}function et(t,o){var n,r;const i=o&&"object"==typeof o;t=i&&"number"==typeof o.status?o.status:void 0===t?-1:t;const s=i&&"string"==typeof o.message?o.message:""+o;(o=i?o:Ue.ExitStatus?function(e,t){const o=new Ue.ExitStatus(e);return o.message=t,o.toString=()=>t,o}(t,s):new Error("Exit with code "+t+" "+s)).status=t,o.message||(o.message=s);const a=""+(o.stack||(new Error).stack);try{Object.defineProperty(o,"stack",{get:()=>a})}catch(e){}const l=!!o.silent;if(o.silent=!0,Je())Ne.diagnosticTracing&&h("mono_exit called after exit");else{try{Be.onAbort==Ye&&(Be.onAbort=Ge),Be.onExit==Xe&&(Be.onExit=Ke),Ce&&(globalThis.removeEventListener("unhandledrejection",ot),globalThis.removeEventListener("error",nt)),Ue.runtimeReady?(Ue.jiterpreter_dump_stats&&Ue.jiterpreter_dump_stats(!1),0===t&&(null===(n=Ne.config)||void 0===n?void 0:n.interopCleanupOnExit)&&Ue.forceDisposeProxies(!0,!0),e&&0!==t&&(null===(r=Ne.config)||void 0===r?void 0:r.dumpThreadsOnNonZeroExit)&&Ue.dumpThreads()):(Ne.diagnosticTracing&&h(`abort_startup, reason: ${o}`),function(e){Ne.allDownloadsQueued.promise_control.reject(e),Ne.allDownloadsFinished.promise_control.reject(e),Ne.afterConfigLoaded.promise_control.reject(e),Ne.wasmCompilePromise.promise_control.reject(e),Ne.runtimeModuleLoaded.promise_control.reject(e),Ue.dotnetReady&&(Ue.dotnetReady.promise_control.reject(e),Ue.afterInstantiateWasm.promise_control.reject(e),Ue.beforePreInit.promise_control.reject(e),Ue.afterPreInit.promise_control.reject(e),Ue.afterPreRun.promise_control.reject(e),Ue.beforeOnRuntimeInitialized.promise_control.reject(e),Ue.afterOnRuntimeInitialized.promise_control.reject(e),Ue.afterPostRun.promise_control.reject(e))}(o))}catch(e){y("mono_exit A failed",e)}try{l||(function(e,t){if(0!==e&&t){const e=Ue.ExitStatus&&t instanceof Ue.ExitStatus?h:v;"string"==typeof t?e(t):(void 0===t.stack&&(t.stack=(new Error).stack+""),t.message?e(Ue.stringify_as_error_with_stack?Ue.stringify_as_error_with_stack(t.message+"\n"+t.stack):t.message+"\n"+t.stack):e(JSON.stringify(t)))}!Pe&&Ne.config&&(Ne.config.logExitCode?Ne.config.forwardConsoleLogsToWS?S("WASM EXIT "+e):b("WASM EXIT "+e):Ne.config.forwardConsoleLogsToWS&&S())}(t,o),function(e){if(Ce&&!Pe&&Ne.config&&Ne.config.appendElementOnExit&&document){const t=document.createElement("label");t.id="tests_done",0!==e&&(t.style.background="red"),t.innerHTML=""+e,document.body.appendChild(t)}}(t))}catch(e){y("mono_exit B failed",e)}Ne.exitCode=t,Ne.exitReason||(Ne.exitReason=o),!Pe&&Ue.runtimeReady&&Be.runtimeKeepalivePop()}if(Ne.config&&Ne.config.asyncFlushOnExit&&0===t)throw(async()=>{try{await async function(){try{const e=await import(/*! webpackIgnore: true */"process"),t=e=>new Promise(((t,o)=>{e.on("error",o),e.end("","utf8",t)})),o=t(e.stderr),n=t(e.stdout);let r;const i=new Promise((e=>{r=setTimeout((()=>e("timeout")),1e3)}));await Promise.race([Promise.all([n,o]),i]),clearTimeout(r)}catch(e){v(`flushing std* streams failed: ${e}`)}}()}finally{tt(t,o)}})(),o;tt(t,o)}function tt(e,t){if(Pe&&Ue.runtimeReady&&Ue.nativeAbort)throw Ue.nativeAbort(t),t;if(Ue.runtimeReady&&Ue.nativeExit)try{Ue.nativeExit(e)}catch(e){!Ue.ExitStatus||e instanceof Ue.ExitStatus||y("set_exit_code_and_quit_now failed: "+e.toString())}if(0!==e||!Ce)throw De&&ze.process?ze.process.exit(e):Ue.quit&&Ue.quit(e,t),t}function ot(e){rt(e,e.reason,"rejection")}function nt(e){rt(e,e.error,"error")}function rt(e,t,o){e.preventDefault();try{t||(t=new Error("Unhandled "+o)),void 0===t.stack&&(t.stack=(new Error).stack),t.stack=t.stack+"",t.silent||(v("Unhandled error:",t),et(1,t))}catch(e){}}!function(e){if(We)throw new Error("Loader module already loaded");We=!0,Ue=e.runtimeHelpers,Ne=e.loaderHelpers,Le=e.diagnosticHelpers,$e=e.api,ze=e.internal,Object.assign($e,{INTERNAL:ze,invokeLibraryInitializers:ve}),Object.assign(e.module,{config:Ee(Fe,{environmentVariables:{}})});const r={mono_wasm_bindings_is_ready:!1,config:e.module.config,diagnosticTracing:!1,nativeAbort:e=>{throw e||new Error("abort")},nativeExit:e=>{throw new Error("exit:"+e)}},l={gitHash:"dc5fd7a8dce8309e4add8fd4bd5d8718f221b15a",config:e.module.config,diagnosticTracing:!1,maxParallelDownloads:16,enableDownloadRetry:!0,_loaded_files:[],loadedFiles:[],loadedAssemblies:[],libraryInitializers:[],workerNextNumber:1,actual_downloaded_assets_count:0,actual_instantiated_assets_count:0,expected_downloaded_assets_count:0,expected_instantiated_assets_count:0,afterConfigLoaded:i(),allDownloadsQueued:i(),allDownloadsFinished:i(),wasmCompilePromise:i(),runtimeModuleLoaded:i(),loadingWorkers:i(),is_exited:Je,is_runtime_running:He,assert_runtime_running:Ze,mono_exit:et,createPromiseController:i,getPromiseController:s,assertIsControllablePromise:a,mono_download_assets:re,resolve_single_asset_path:oe,setup_proxy_console:R,set_thread_prefix:p,installUnhandledErrorHandler:Qe,retrieve_asset_download:ae,invokeLibraryInitializers:ve,isDebuggingSupported:Ae,exceptions:t,simd:n,relaxedSimd:o};Object.assign(Ue,r),Object.assign(Ne,l)}(Ve);let it,st,at,lt=!1,ct=!1;async function dt(e){if(!ct){if(ct=!0,Ce&&Ne.config.forwardConsoleLogsToWS&&void 0!==globalThis.WebSocket&&R("main",globalThis.console,globalThis.location.origin),Be||qe(!1,"Null moduleConfig"),Ne.config||qe(!1,"Null moduleConfig.config"),"function"==typeof e){const t=e(Ve.api);if(t.ready)throw new Error("Module.ready couldn't be redefined.");Object.assign(Be,t),Te(Be,t)}else{if("object"!=typeof e)throw new Error("Can't use moduleFactory callback of createDotnetRuntime function.");Te(Be,e)}await async function(e){if(De){const e=await import(/*! webpackIgnore: true */"process"),t=14;if(e.versions.node.split(".")[0]<t)throw new Error(`NodeJS at '${e.execPath}' has too low version '${e.versions.node}', please use at least ${t}. See also https://aka.ms/dotnet-wasm-features`)}const t=/*! webpackIgnore: true */import.meta.url,o=t.indexOf("?");var n;if(o>0&&(Ne.modulesUniqueQuery=t.substring(o)),Ne.scriptUrl=t.replace(/\\/g,"/").replace(/[?#].*/,""),Ne.scriptDirectory=(n=Ne.scriptUrl).slice(0,n.lastIndexOf("/"))+"/",Ne.locateFile=e=>"URL"in globalThis&&globalThis.URL!==P?new URL(e,Ne.scriptDirectory).toString():L(e)?e:Ne.scriptDirectory+e,Ne.fetch_like=C,Ne.out=console.log,Ne.err=console.error,Ne.onDownloadResourceProgress=e.onDownloadResourceProgress,Ce&&globalThis.navigator){const e=globalThis.navigator,t=e.userAgentData&&e.userAgentData.brands;t&&t.length>0?Ne.isChromium=t.some((e=>"Google Chrome"===e.brand||"Microsoft Edge"===e.brand||"Chromium"===e.brand)):e.userAgent&&(Ne.isChromium=e.userAgent.includes("Chrome"),Ne.isFirefox=e.userAgent.includes("Firefox"))}ze.require=De?await import(/*! webpackIgnore: true */"module").then((e=>e.createRequire(/*! webpackIgnore: true */import.meta.url))):Promise.resolve((()=>{throw new Error("require not supported")})),void 0===globalThis.URL&&(globalThis.URL=P)}(Be)}}async function ut(e){return await dt(e),Ge=Be.onAbort,Ke=Be.onExit,Be.onAbort=Ye,Be.onExit=Xe,Be.ENVIRONMENT_IS_PTHREAD?async function(){(function(){const e=new MessageChannel,t=e.port1,o=e.port2;t.addEventListener("message",(e=>{var n,r;n=JSON.parse(e.data.config),r=JSON.parse(e.data.monoThreadInfo),lt?Ne.diagnosticTracing&&h("mono config already received"):(Ee(Ne.config,n),Ue.monoThreadInfo=r,je(),Ne.diagnosticTracing&&h("mono config received"),lt=!0,Ne.afterConfigLoaded.promise_control.resolve(Ne.config),Ce&&n.forwardConsoleLogsToWS&&void 0!==globalThis.WebSocket&&Ne.setup_proxy_console("worker-idle",console,globalThis.location.origin)),t.close(),o.close()}),{once:!0}),t.start(),self.postMessage({[l]:{monoCmd:"preload",port:o}},[o])})(),await Ne.afterConfigLoaded.promise,function(){const e=Ne.config;e.assets||qe(!1,"config.assets must be defined");for(const t of e.assets)ee(t),K[t.behavior]&&F.push(t)}(),setTimeout((async()=>{try{await re()}catch(e){et(1,e)}}),0);const e=ft(),t=await Promise.all(e);return await mt(t),Be}():async function(){var e;await Se(Be),se();const t=ft();(async function(){try{const e=oe("dotnetwasm");await le(e),e&&e.pendingDownloadInternal&&e.pendingDownloadInternal.response||qe(!1,"Can't load dotnet.native.wasm");const t=await e.pendingDownloadInternal.response,o=t.headers&&t.headers.get?t.headers.get("Content-Type"):void 0;let n;if("function"==typeof WebAssembly.compileStreaming&&"application/wasm"===o)n=await WebAssembly.compileStreaming(t);else{Ce&&"application/wasm"!==o&&y('WebAssembly resource does not have the expected content type "application/wasm", so falling back to slower ArrayBuffer instantiation.');const e=await t.arrayBuffer();Ne.diagnosticTracing&&h("instantiate_wasm_module buffered"),n=Me?await Promise.resolve(new WebAssembly.Module(e)):await WebAssembly.compile(e)}e.pendingDownloadInternal=null,e.pendingDownload=null,e.buffer=null,e.moduleExports=null,Ne.wasmCompilePromise.promise_control.resolve(n)}catch(e){Ne.wasmCompilePromise.promise_control.reject(e)}})(),setTimeout((async()=>{try{k(),function(){const e=oe("js-module-threads"),t=[];for(let o=0;o<Ne.config.pthreadPoolInitialSize;o++){const o=Ne.workerNextNumber++,n=new Worker(e.resolvedUrl,{name:"dotnet-worker-"+o.toString().padStart(3,"0"),type:"module"});n.info={workerNumber:o,pthreadId:0,reuseCount:0,updateCount:0,threadPrefix:"          -    ",threadName:"emscripten-pool"},t.push(n)}Ne.loadingWorkers.promise_control.resolve(t)}(),await re()}catch(e){et(1,e)}}),0);const o=await Promise.all(t);return await mt(o),await Ue.dotnetReady.promise,await ye(null===(e=Ne.config.resources)||void 0===e?void 0:e.modulesAfterRuntimeReady),await ve("onRuntimeReady",[Ve.api]),$e}()}function ft(){const e=oe("js-module-runtime"),t=oe("js-module-native");if(it&&st)return[it,st,at];"object"==typeof e.moduleExports?it=e.moduleExports:(Ne.diagnosticTracing&&h(`Attempting to import '${e.resolvedUrl}' for ${e.name}`),it=import(/*! webpackIgnore: true */e.resolvedUrl)),"object"==typeof t.moduleExports?st=t.moduleExports:(Ne.diagnosticTracing&&h(`Attempting to import '${t.resolvedUrl}' for ${t.name}`),st=import(/*! webpackIgnore: true */t.resolvedUrl));const o=te("js-module-diagnostics");return o&&("object"==typeof o.moduleExports?at=o.moduleExports:(Ne.diagnosticTracing&&h(`Attempting to import '${o.resolvedUrl}' for ${o.name}`),at=import(/*! webpackIgnore: true */o.resolvedUrl))),[it,st,at]}async function mt(e){const{initializeExports:t,initializeReplacements:o,configureRuntimeStartup:n,configureEmscriptenStartup:r,configureWorkerStartup:i,setRuntimeGlobals:s,passEmscriptenInternals:a}=e[0],{default:l}=e[1],c=e[2];s(Ve),t(Ve),c&&c.setRuntimeGlobals(Ve),await n(Be),Ne.runtimeModuleLoaded.promise_control.resolve(),l((e=>(Object.assign(Be,{ready:e.ready,__dotnet_runtime:{initializeReplacements:o,configureEmscriptenStartup:r,configureWorkerStartup:i,passEmscriptenInternals:a}}),Be))).catch((e=>{if(e.message&&e.message.toLowerCase().includes("out of memory"))throw new Error(".NET runtime has failed to start, because too much memory was requested. Please decrease the memory by adjusting EmccMaximumHeapSize. See also https://aka.ms/dotnet-wasm-features");throw e}))}const gt=new class{withModuleConfig(e){try{return Te(Be,e),this}catch(e){throw et(1,e),e}}withOnConfigLoaded(e){try{return Te(Be,{onConfigLoaded:e}),this}catch(e){throw et(1,e),e}}withConsoleForwarding(){try{return Ee(Fe,{forwardConsoleLogsToWS:!0}),this}catch(e){throw et(1,e),e}}withExitOnUnhandledError(){try{return Ee(Fe,{exitOnUnhandledError:!0}),Qe(),this}catch(e){throw et(1,e),e}}withAsyncFlushOnExit(){try{return Ee(Fe,{asyncFlushOnExit:!0}),this}catch(e){throw et(1,e),e}}withExitCodeLogging(){try{return Ee(Fe,{logExitCode:!0}),this}catch(e){throw et(1,e),e}}withElementOnExit(){try{return Ee(Fe,{appendElementOnExit:!0}),this}catch(e){throw et(1,e),e}}withInteropCleanupOnExit(){try{return Ee(Fe,{interopCleanupOnExit:!0}),this}catch(e){throw et(1,e),e}}withDumpThreadsOnNonZeroExit(){try{return Ee(Fe,{dumpThreadsOnNonZeroExit:!0}),this}catch(e){throw et(1,e),e}}withWaitingForDebugger(e){try{return Ee(Fe,{waitForDebugger:e}),this}catch(e){throw et(1,e),e}}withInterpreterPgo(e,t){try{return Ee(Fe,{interpreterPgo:e,interpreterPgoSaveDelay:t}),Fe.runtimeOptions?Fe.runtimeOptions.push("--interp-pgo-recording"):Fe.runtimeOptions=["--interp-pgo-recording"],this}catch(e){throw et(1,e),e}}withConfig(e){try{return Ee(Fe,e),this}catch(e){throw et(1,e),e}}withConfigSrc(e){try{return e&&"string"==typeof e||qe(!1,"must be file path or URL"),Te(Be,{configSrc:e}),this}catch(e){throw et(1,e),e}}withVirtualWorkingDirectory(e){try{return e&&"string"==typeof e||qe(!1,"must be directory path"),Ee(Fe,{virtualWorkingDirectory:e}),this}catch(e){throw et(1,e),e}}withEnvironmentVariable(e,t){try{const o={};return o[e]=t,Ee(Fe,{environmentVariables:o}),this}catch(e){throw et(1,e),e}}withEnvironmentVariables(e){try{return e&&"object"==typeof e||qe(!1,"must be dictionary object"),Ee(Fe,{environmentVariables:e}),this}catch(e){throw et(1,e),e}}withDiagnosticTracing(e){try{return"boolean"!=typeof e&&qe(!1,"must be boolean"),Ee(Fe,{diagnosticTracing:e}),this}catch(e){throw et(1,e),e}}withDebugging(e){try{return null!=e&&"number"==typeof e||qe(!1,"must be number"),Ee(Fe,{debugLevel:e}),this}catch(e){throw et(1,e),e}}withApplicationArguments(...e){try{return e&&Array.isArray(e)||qe(!1,"must be array of strings"),Ee(Fe,{applicationArguments:e}),this}catch(e){throw et(1,e),e}}withRuntimeOptions(e){try{return e&&Array.isArray(e)||qe(!1,"must be array of strings"),Fe.runtimeOptions?Fe.runtimeOptions.push(...e):Fe.runtimeOptions=e,this}catch(e){throw et(1,e),e}}withMainAssembly(e){try{return Ee(Fe,{mainAssemblyName:e}),this}catch(e){throw et(1,e),e}}withApplicationArgumentsFromQuery(){try{if(!globalThis.window)throw new Error("Missing window to the query parameters from");if(void 0===globalThis.URLSearchParams)throw new Error("URLSearchParams is supported");const e=new URLSearchParams(globalThis.window.location.search).getAll("arg");return this.withApplicationArguments(...e)}catch(e){throw et(1,e),e}}withApplicationEnvironment(e){try{return Ee(Fe,{applicationEnvironment:e}),this}catch(e){throw et(1,e),e}}withApplicationCulture(e){try{return Ee(Fe,{applicationCulture:e}),this}catch(e){throw et(1,e),e}}withResourceLoader(e){try{return Ne.loadBootResource=e,this}catch(e){throw et(1,e),e}}async download(){try{await async function(){dt(Be),await Se(Be),se(),k(),re(),await Ne.allDownloadsFinished.promise}()}catch(e){throw et(1,e),e}}async create(){try{return this.instance||(this.instance=await async function(){return await ut(Be),Ve.api}()),this.instance}catch(e){throw et(1,e),e}}async run(){try{return Be.config||qe(!1,"Null moduleConfig.config"),this.instance||await this.create(),this.instance.runMainAndExit()}catch(e){throw et(1,e),e}}},pt=et,ht=ut;Me||"function"==typeof globalThis.URL||qe(!1,"This browser/engine doesn't support URL API. Please use a modern version. See also https://aka.ms/dotnet-wasm-features"),"function"!=typeof globalThis.BigInt64Array&&qe(!1,"This browser/engine doesn't support BigInt64Array API. Please use a modern version. See also https://aka.ms/dotnet-wasm-features"),(Me||De)&&qe(!1,"This build of dotnet is multi-threaded, it doesn't support shell environments like V8 or NodeJS. See also https://aka.ms/dotnet-wasm-features"),void 0===globalThis.SharedArrayBuffer&&qe(!1,"SharedArrayBuffer is not enabled on this page. Please use a modern browser and set Cross-Origin-Opener-Policy and Cross-Origin-Embedder-Policy http headers. See also https://aka.ms/dotnet-wasm-features"),"function"!=typeof globalThis.EventTarget&&qe(!1,"This browser/engine doesn't support EventTarget API. Please use a modern version. See also https://aka.ms/dotnet-wasm-features"),gt.withConfig(/*json-start*/{
  "mainAssemblyName": "CelesteLoader",
  "resources": {
    "hash": "sha256-vufwl924GqlB+QfMeQOtu7gB0aX8q15q+UU51wIpol0=",
    "jsModuleNative": [
      {
        "name": "dotnet.native.j5v2s4hecp.js"
      }
    ],
    "jsModuleRuntime": [
      {
        "name": "dotnet.runtime.tjpcm9se4j.js"
      }
    ],
    "jsModuleWorker": [
      {
        "name": "dotnet.native.worker.mjs"
      }
    ],
    "wasmNative": [
      {
        "name": "dotnet.native.w4yfy81t7c.wasm",
        "hash": "sha256-cNOkL+/WZUCYWMBjvEZADDcIw0l47fmEyGxLe1cNipA=",
        "cache": "force-cache"
      }
    ],
    "icu": [
      {
        "virtualPath": "icudt_CJK.dat",
        "name": "icudt_CJK.tjcz0u77k5.dat",
        "hash": "sha256-SZLtQnRc0JkwqHab0VUVP7T3uBPSeYzxzDnpxPpUnHk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "icudt_EFIGS.dat",
        "name": "icudt_EFIGS.tptq2av103.dat",
        "hash": "sha256-8fItetYY8kQ0ww6oxwTLiT3oXlBwHKumbeP2pRF4yTc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "icudt_no_CJK.dat",
        "name": "icudt_no_CJK.lfu7j35m59.dat",
        "hash": "sha256-L7sV7NEYP37/Qr2FPCePo5cJqRgTXRwGHuwF5Q+0Nfs=",
        "cache": "force-cache"
      }
    ],
    "coreAssembly": [
      {
        "virtualPath": "Celeste.Wasm.mm.dll",
        "name": "Celeste.Wasm.mm.ubh2gjnetc.dll",
        "hash": "sha256-Mw0OCzYi62lH6bx60oE37a+7ML48TzK9G7B9CE87AOc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "CelesteLoader.dll",
        "name": "CelesteLoader.dr8i5i78r8.dll",
        "hash": "sha256-J+YMu8v9fa/AAUd+G5jUDaQQBWrbkLUs6XXDL40v5Tc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "DepotDownloader.dll",
        "name": "DepotDownloader.nhdf95wbj1.dll",
        "hash": "sha256-C4+sSfpgNstfAPLSC2qS84HVSBXcRlUJlSTXV3J7cnU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "DnsOverHttps.dll",
        "name": "DnsOverHttps.ayxy9yxpns.dll",
        "hash": "sha256-nl5K2mCEClaGq3gAlLwx4slhXg7RtPB2NZ+7rqmxMnw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "FNA.dll",
        "name": "FNA.t7z7m0d8s2.dll",
        "hash": "sha256-96usegGrcKSuQsGg5pNjzm34JW+ZGJ1t9d1WDiFw4pI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Jdenticon.dll",
        "name": "Jdenticon.nrz6664d5v.dll",
        "hash": "sha256-kxiVnw5GqXBtusdbfqhjP/Egsy64ku9w+qzuTslVkgc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "KeraLua.dll",
        "name": "KeraLua.bs2ow45vul.dll",
        "hash": "sha256-5Rv5tDk8v9mUCDjBLObWilbW/gYsIrGpdcDbKTUbeas=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "MAB.DotIgnore.dll",
        "name": "MAB.DotIgnore.fguwxk910c.dll",
        "hash": "sha256-L8PfoJK3tDtM0nd/R95Ez78iL/C4rGUQbEF+qeP2nxM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.CSharp.dll",
        "name": "Microsoft.CSharp.j2vy9drzo8.dll",
        "hash": "sha256-yz47rAmN6LMJeoZ7a9VLaOK79ka4qvQXFWofVkXkjm8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.VisualBasic.Core.dll",
        "name": "Microsoft.VisualBasic.Core.8qwxxj3ri7.dll",
        "hash": "sha256-EP9jofGC5gKJR4GD6IQyWXUtxIXat6GMjNXLevwrW+I=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.VisualBasic.dll",
        "name": "Microsoft.VisualBasic.l46z9aas2w.dll",
        "hash": "sha256-O0UeyuhJpl+zvz01AAo33iymXGM6cZ0W5LuoTULpxBo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Win32.Primitives.dll",
        "name": "Microsoft.Win32.Primitives.954o4esyad.dll",
        "hash": "sha256-/eYkWRpht5MxYJLXOekZnQ8TZs0/2uUih/VLZIRVjkw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Win32.Registry.dll",
        "name": "Microsoft.Win32.Registry.6hq7ni40kx.dll",
        "hash": "sha256-ysCWC2qdD4gqSHcLH3e2ZTpg5tG7+bpVnzyT4cOd5JM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Mono.Cecil.dll",
        "name": "Mono.Cecil.7r7tpqbd1c.dll",
        "hash": "sha256-gx3Kd0cNhctv++owctqno99bfJ/P2cP0NnSpvpnUv88=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Mono.Cecil.Mdb.dll",
        "name": "Mono.Cecil.Mdb.4lzcss0sz9.dll",
        "hash": "sha256-KMs2eXK9wc1D5ABjBq8v2W039O1LI57pDh3HI3qTr38=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Mono.Cecil.Pdb.dll",
        "name": "Mono.Cecil.Pdb.p9wkptuf3z.dll",
        "hash": "sha256-ozIzJjP7yyDo1Q5JtNt70VV3IUFxIs8MX0xC8jMjkdA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Mono.Cecil.Rocks.dll",
        "name": "Mono.Cecil.Rocks.143a4x1ehj.dll",
        "hash": "sha256-v5kvPc42TrzDIA+ngy7wfiC04tvDqKYhPOROPSOduYQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "MonoMod.Backports.dll",
        "name": "MonoMod.Backports.ybwrfrb542.dll",
        "hash": "sha256-knhDb2K7J030k4KYwqEpSXrYPVrDhfeHJmA1JjJYn9Q=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "MonoMod.Core.dll",
        "name": "MonoMod.Core.xpexr5aupj.dll",
        "hash": "sha256-O+gnIUywL7iLe4guZmlKtfyBOlKVEYfW0DEyGnd/YQA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "MonoMod.ILHelpers.dll",
        "name": "MonoMod.ILHelpers.c0ze9igbpn.dll",
        "hash": "sha256-RGaceNJoKkWNf9ceTnQ0GL7Nqh2f0C8P0ZQTAtaul2M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "MonoMod.Iced.dll",
        "name": "MonoMod.Iced.70kuebwqiy.dll",
        "hash": "sha256-OeVfs//moQHmnUiGZvikcuqP9+uiKEra0x2wUwDEue0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "MonoMod.Patcher.dll",
        "name": "MonoMod.Patcher.2hml3dqw0u.dll",
        "hash": "sha256-+sNhF1rTCJ3VVMPILJ3tLdDkd6xAWQ2qutr4jPacdd8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "MonoMod.RuntimeDetour.HookGen.dll",
        "name": "MonoMod.RuntimeDetour.HookGen.dim7x1d2mg.dll",
        "hash": "sha256-tSJkLWmfTWRegPDJrXkmFDDBAw6oOzSHBXzlXxX339Y=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "MonoMod.RuntimeDetour.dll",
        "name": "MonoMod.RuntimeDetour.ovvrjiow0t.dll",
        "hash": "sha256-JNX/u93eXRY76MYIrfEAAZUV0K6BSbcojGfhY5qlNnc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "MonoMod.Utils.dll",
        "name": "MonoMod.Utils.g87bsxbtze.dll",
        "hash": "sha256-oFbdu1grRTyR1bSwCB9mAVV+hx8CMldjgLrYUM4kd+8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NETCoreifier.dll",
        "name": "NETCoreifier.4ojhx4xaaa.dll",
        "hash": "sha256-LKHJ14b1birY/RQ8fx9uKo9ryHFsnguoupyh7VyBX9w=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NLua.dll",
        "name": "NLua.r7yct28dhv.dll",
        "hash": "sha256-N32kWG/TC6MgjUNqAlvsCD4E4obJRB/Azljlat4uOig=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Newtonsoft.Json.dll",
        "name": "Newtonsoft.Json.a56zs13vug.dll",
        "hash": "sha256-IsZJ91/OW+fHzNqIgEc7Y072ns8z9dGritiSyvR9Wgc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "QRCoder.dll",
        "name": "QRCoder.wpaady2972.dll",
        "hash": "sha256-PFZ09nqX/Z7TjdU32JLunv0yivrU1sHWf5n94sEI3Vc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "SteamKit2.dll",
        "name": "SteamKit2.aqp9hhfh11.dll",
        "hash": "sha256-flD3ioQ29Rq//btF7Wz4ovgiXG6d0TaWVIhC7sIMTgg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Steamworks.NET.dll",
        "name": "Steamworks.NET.pfu1p0ctmy.dll",
        "hash": "sha256-VQmC6aACdm1wvATy1UabzHF59x2Px5gKPirB7wRswVY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.AppContext.dll",
        "name": "System.AppContext.p47m0w8yr4.dll",
        "hash": "sha256-mU0MhCZIW6RXNEKIRHMlLRx31z1tRwJoKXY3xVeCiEw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Buffers.dll",
        "name": "System.Buffers.xhmwojafy3.dll",
        "hash": "sha256-GyKNriHz7yWfcz1SuEXg1blMfXaTJSOpgZkyqSzh72k=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.dll",
        "name": "System.Collections.5lllj0gqvn.dll",
        "hash": "sha256-idNU2uLj00QDsoQpeI2HMak5NhbF1S1RfpRgkoSeQro=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.Concurrent.dll",
        "name": "System.Collections.Concurrent.g07fe2p0yy.dll",
        "hash": "sha256-wIjmv1x5m7b2QPimjXsLCM2fWoVvHaDxjk4aRGHmTKk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.Immutable.dll",
        "name": "System.Collections.Immutable.dho4yabhop.dll",
        "hash": "sha256-D/H2305QjiuZKBhXACDtDYSMcmJAR3FYwiJd7S73eoI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.NonGeneric.dll",
        "name": "System.Collections.NonGeneric.q3rk8b6eet.dll",
        "hash": "sha256-ykULI1HZeqmhzSt6ArRVHmetBb54dgAAaMuHQO59+L8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.Specialized.dll",
        "name": "System.Collections.Specialized.lu1uddds7l.dll",
        "hash": "sha256-3bVKYJJPbu5b2FklzesXWMxKXJgd+LhoppM/JhbaKuc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.Annotations.dll",
        "name": "System.ComponentModel.Annotations.4j3j8mevf0.dll",
        "hash": "sha256-YKcfQwgHsj3Avq9V67oneiUn2PIM4mo1fp4Hca9QDmA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.DataAnnotations.dll",
        "name": "System.ComponentModel.DataAnnotations.1jf2c9ougx.dll",
        "hash": "sha256-HQuepTCChZ87Z2m3OltJfHsVZmb1hHHk0EiypRz3Vqk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.EventBasedAsync.dll",
        "name": "System.ComponentModel.EventBasedAsync.00ghjigtdo.dll",
        "hash": "sha256-QCbed1EbkpYslSliQ2JXgFvTxDXes/TaT9YhVcl6dxA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.Primitives.dll",
        "name": "System.ComponentModel.Primitives.451fghecw8.dll",
        "hash": "sha256-iIfnb74QwXcNHe0RjxPGgzs7EfbKr1Th4JOEtqkXogY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.TypeConverter.dll",
        "name": "System.ComponentModel.TypeConverter.w6r7ljaxg7.dll",
        "hash": "sha256-mDmtTYjgdmuwetaTAdyh3C0efxZlQ9c1j4/lqsMaRQg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.dll",
        "name": "System.ComponentModel.b4mc5qpia3.dll",
        "hash": "sha256-BSjDEtn/eMj4FD8/LHtTyDBnoSHoJcXmrpQ07ZZVWsI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Configuration.dll",
        "name": "System.Configuration.uvmbdbimyd.dll",
        "hash": "sha256-miF5COqG/fR2nL9CJYlJ2STL3ctue1a3YAIg1y+CNW4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Console.dll",
        "name": "System.Console.chpwjx2rvr.dll",
        "hash": "sha256-AMDoa6AYQseSZVit+CBCZvFyJOioL4KL3fRhdB47VrI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Core.dll",
        "name": "System.Core.b1cfsc9bq2.dll",
        "hash": "sha256-r4Z+k4thMbJ5iSF5cXqrQFEJ3SNh04tPAuueGGb9Ah4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Data.dll",
        "name": "System.Data.9gy7mg65zc.dll",
        "hash": "sha256-KWCYAGKjvLFyGjvmIkOTR1FcfaDNuKsRCTD1V1DO7aw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Data.Common.dll",
        "name": "System.Data.Common.hda54p7oxh.dll",
        "hash": "sha256-O0Q3ybUTdB2K/TedmHCX5aamvdfFqtMC9FnbUjzckjE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Data.DataSetExtensions.dll",
        "name": "System.Data.DataSetExtensions.sfn7wqsyn6.dll",
        "hash": "sha256-aHo3BjFrSPUSBqOIpmaFL9hBCaBB2+vzqWfWDCMqnlo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Contracts.dll",
        "name": "System.Diagnostics.Contracts.lxhlwn7os0.dll",
        "hash": "sha256-92V8SWo8a8/7kdJs2aZ+DoD+zZRBxykDm/E5/1aEC9Y=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Debug.dll",
        "name": "System.Diagnostics.Debug.5g3a7ytmby.dll",
        "hash": "sha256-C1Ga8Q9eAGfphaVY/H4ZAS7/+jVAYuBdAM/3c8/EgGg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.DiagnosticSource.dll",
        "name": "System.Diagnostics.DiagnosticSource.xzefj5xtg3.dll",
        "hash": "sha256-U4+OTyG9lQvbKOKzhBbiCqfPcCOjC2uNPKH9uyVINY0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.FileVersionInfo.dll",
        "name": "System.Diagnostics.FileVersionInfo.tdd5jrxj0e.dll",
        "hash": "sha256-/x0uiS/xKdOri5ZAiZxCa0GHbUv+yyWU0TgJBhA6Ubg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Process.dll",
        "name": "System.Diagnostics.Process.sd3d1op743.dll",
        "hash": "sha256-oFwxm8xdyYOg8GdZIDK3ZifV+EeJAqTCzYPFX1eXF0w=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.StackTrace.dll",
        "name": "System.Diagnostics.StackTrace.nihd60711w.dll",
        "hash": "sha256-EfAThzm1Z8HvSYbm5jPCm8mxEHG1d9rfxXYdy2R3JVA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.TextWriterTraceListener.dll",
        "name": "System.Diagnostics.TextWriterTraceListener.6tk12mdbbu.dll",
        "hash": "sha256-ZvZ9Ns5itkysbEXO1egt3VdtxwVuMyLe1q3X0qz5fjg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Tools.dll",
        "name": "System.Diagnostics.Tools.tm7ishylx6.dll",
        "hash": "sha256-2yisp78hX8D84M1M5PiblWcDB+Gt1/6ZADidPaNDvv8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.TraceSource.dll",
        "name": "System.Diagnostics.TraceSource.hq4hv4qomz.dll",
        "hash": "sha256-OeHAP01MgvRa1MZF96jBQUI9JV7zpYJ/7sXVJsjTejg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Tracing.dll",
        "name": "System.Diagnostics.Tracing.8y42lmauvb.dll",
        "hash": "sha256-cA3dve6aZ4Lb79KohKVn6ery4o0Jd6P/Er7g+LRihrg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Drawing.Primitives.dll",
        "name": "System.Drawing.Primitives.zncexwv1zy.dll",
        "hash": "sha256-YVeHGylcfQbN9/oBU2+vR5xOjL9rcWQMqZHq8nPbYnU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Drawing.dll",
        "name": "System.Drawing.prnsx6nurj.dll",
        "hash": "sha256-VfC+js1kA1YYY6dlaFqUSbioRlli0TVsbHwuwgASiX8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Dynamic.Runtime.dll",
        "name": "System.Dynamic.Runtime.e8tkoh6b37.dll",
        "hash": "sha256-AhOf7PIM5wjL52cBwndDMcJNKgCTwC6Q0QQQmnl4HJI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Formats.Asn1.dll",
        "name": "System.Formats.Asn1.oi5q17ivzo.dll",
        "hash": "sha256-kGT9uNBKrAylHuPtZINabAEobgIqLRCZkfncvXMLWUI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Formats.Tar.dll",
        "name": "System.Formats.Tar.mwj2iuz304.dll",
        "hash": "sha256-urQXpbUo6ebcLnirczZG3i1bgh1o1T1PGPFqI1qvCaE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Globalization.dll",
        "name": "System.Globalization.3nmozrb8op.dll",
        "hash": "sha256-n4dly7tuKjQ6ygpnby47xeOwG55aNQC42Kk2xg5dNXY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Globalization.Calendars.dll",
        "name": "System.Globalization.Calendars.as188w5hew.dll",
        "hash": "sha256-9iwN3aWEU/XSXgIGO/X7manBof29sr3gRgav4Wn9C80=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Globalization.Extensions.dll",
        "name": "System.Globalization.Extensions.ngm2ngudio.dll",
        "hash": "sha256-N7UomNMjvEVqstC4xCw/ySCFcOXsTtXrYrzZWGLR6JA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Compression.dll",
        "name": "System.IO.Compression.0spod18c6g.dll",
        "hash": "sha256-gOlSc1zVtL5XTJHzCoE7uOQnUfY+BhIclGX24S4WxYI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Compression.Brotli.dll",
        "name": "System.IO.Compression.Brotli.o53t9sun05.dll",
        "hash": "sha256-PO7U0q5TgKRIVBg5eWaz5DoCGPPgnd7JUWMUktPPgc8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Compression.FileSystem.dll",
        "name": "System.IO.Compression.FileSystem.vln5lh6vqy.dll",
        "hash": "sha256-wz8pqL5vjK5uy2hBy4oIZk9l6uuY0xAX668NjnHhnjE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Compression.ZipFile.dll",
        "name": "System.IO.Compression.ZipFile.q31yuex2ud.dll",
        "hash": "sha256-FqDdXzjC3+FUJ8Cc9ZlbDzMxSUkmfuCcsIgBh1g11hs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.FileSystem.AccessControl.dll",
        "name": "System.IO.FileSystem.AccessControl.fnu5al3ovb.dll",
        "hash": "sha256-axgSfPBwsWFAKx14QVIst0jB5KLa6IBIrrQkGf67XdI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.FileSystem.DriveInfo.dll",
        "name": "System.IO.FileSystem.DriveInfo.8d1jmniatz.dll",
        "hash": "sha256-VEDzJk1BAZ7KSeyYMCG1CDKyDHTP4IogU6JMKJsyxN8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.FileSystem.Primitives.dll",
        "name": "System.IO.FileSystem.Primitives.fk5x04ht9u.dll",
        "hash": "sha256-kYXvy+7mmS/FpHLobHCACqgiRmuQxKhy6W1dqsagQg8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.FileSystem.Watcher.dll",
        "name": "System.IO.FileSystem.Watcher.yvpns4yrbn.dll",
        "hash": "sha256-MhHPb+3P88H97psUxxGvHCATySOIomkWiSW47K0zyIY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.FileSystem.dll",
        "name": "System.IO.FileSystem.vhz2sqrhaa.dll",
        "hash": "sha256-zauQontBrly7PO+eyxpL0u6Di2A3BmSeAlvOpgWorLY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Hashing.dll",
        "name": "System.IO.Hashing.7odo4y9avc.dll",
        "hash": "sha256-N7emHxxm7BITyv3FSALoJAIrgbDZeNnscyuKp+Neojc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.IsolatedStorage.dll",
        "name": "System.IO.IsolatedStorage.w5ye196ago.dll",
        "hash": "sha256-jBaDQR9JDXehw4adOEBLATmrATJ20o5n+tU8g8vkzso=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.MemoryMappedFiles.dll",
        "name": "System.IO.MemoryMappedFiles.bpg7e4kxfi.dll",
        "hash": "sha256-sdGOzifrP4G2ZuVUQRngSgKcvV9R7Sz9KuD0j6UOvYI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Pipelines.dll",
        "name": "System.IO.Pipelines.pn8naafqzv.dll",
        "hash": "sha256-la3uUuZxLAcJelE+DX1F5dT71UOx1Q2YZrQd58tGMNs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Pipes.dll",
        "name": "System.IO.Pipes.3mevbvnrf4.dll",
        "hash": "sha256-u1K0bvxhNMoXJ+AOp74gyHDvhU56lMkcIm/Cu3YCjnw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Pipes.AccessControl.dll",
        "name": "System.IO.Pipes.AccessControl.tci6ms5xkr.dll",
        "hash": "sha256-kwfMfDBrKav4EybgyYZ68iURxvUDr4CCbWx6cwOZ6TQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.UnmanagedMemoryStream.dll",
        "name": "System.IO.UnmanagedMemoryStream.g4swkml3uq.dll",
        "hash": "sha256-oO3wig99976Oj4Srg7iTKdRy1lw+YeFN/Y7BeGry/Zo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.dll",
        "name": "System.IO.b2pr8xs2t8.dll",
        "hash": "sha256-XdxgbBpnS5K89u+Moq7jGG0Z69iCye5oRf1XKFVuQv4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Linq.AsyncEnumerable.dll",
        "name": "System.Linq.AsyncEnumerable.s1gehfggmt.dll",
        "hash": "sha256-wJW82Rm/pp5xuFBOIC4rmrAevMz1MNo/sR338uWFAkg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Linq.Expressions.dll",
        "name": "System.Linq.Expressions.1vkvgh5gsb.dll",
        "hash": "sha256-o0AD7k+3eI2YTI744EM7/LC7NAjtw6NwNF/sAZYo+MY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Linq.Parallel.dll",
        "name": "System.Linq.Parallel.foox54ytqj.dll",
        "hash": "sha256-LxAmdcpj3DhXKRlharNOVjLaJNg342I0bHWvpWX26Es=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Linq.Queryable.dll",
        "name": "System.Linq.Queryable.dso760s4xg.dll",
        "hash": "sha256-wwCBkSEWZrPCXJvKH8iOPTOF1Fff9ljoF7B3LWLeBUQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Linq.dll",
        "name": "System.Linq.ngjjzpxx60.dll",
        "hash": "sha256-R0AmIKvOSVAjL8RuDbHAl+lsxMZoxsVveoIpEV50AJA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Memory.dll",
        "name": "System.Memory.opvdrtxayu.dll",
        "hash": "sha256-zADcFUgGNRMv2YC/hLm3RGDqqeGcB7KWkTpDTIt4134=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Http.Json.dll",
        "name": "System.Net.Http.Json.3xxrfhgvi2.dll",
        "hash": "sha256-iQiS0cdyKR+1DZ8BBAPLAhufW0UXAg8tFtUpYSRtQwI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Http.dll",
        "name": "System.Net.Http.e7627iewmu.dll",
        "hash": "sha256-6loNoW8Uvr0d/bJZlguo0GqhL7tVY0s7Ydq7OnNEzpg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.HttpListener.dll",
        "name": "System.Net.HttpListener.a561r6odni.dll",
        "hash": "sha256-opdJYDG/SQa3+fBBfHJjgd/XOANFDJ304J1TMdAsGyU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Mail.dll",
        "name": "System.Net.Mail.9x66934y9n.dll",
        "hash": "sha256-jb2w6kSD55hV2iLR9/Runz/2mGzIHkydTnTLdXmMw7k=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.NameResolution.dll",
        "name": "System.Net.NameResolution.5vhvotcfhr.dll",
        "hash": "sha256-MZgpyIDlU9AugkHwPbPbp4lBeY4/+OLh4HYyzNsqCos=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.NetworkInformation.dll",
        "name": "System.Net.NetworkInformation.fdyg6s30eg.dll",
        "hash": "sha256-A/hKgYtUilVjOrilf+/ZlX9w4Y+kv9MadCUeOVd0pvc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Ping.dll",
        "name": "System.Net.Ping.20ch5uihuo.dll",
        "hash": "sha256-AhQre55aCv85ThXcJsLG0cSQp7UnlqzlwYjuUQy+j1Q=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Primitives.dll",
        "name": "System.Net.Primitives.jeltorgcrb.dll",
        "hash": "sha256-ZYpB+pmgKpK44H5UhdLApgF4pFkkG+eb0+2aUrPbT7U=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Quic.dll",
        "name": "System.Net.Quic.x4yvy1rzpo.dll",
        "hash": "sha256-b8vUVIMZa/jLdbY9XVdcjK13aE7coSZvrA0Ii4ifuWw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Requests.dll",
        "name": "System.Net.Requests.avfdr4hlvd.dll",
        "hash": "sha256-lmzmnTaxuvcAghZRg/C8zhdufFZrYARvY0j09TaoUPs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Security.dll",
        "name": "System.Net.Security.mvi1w0tt1j.dll",
        "hash": "sha256-LrprZN/chv+LM7iZUDl+WHSnOVLSiv8EQ4xgs2piOD4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.ServerSentEvents.dll",
        "name": "System.Net.ServerSentEvents.fxo33jf7n2.dll",
        "hash": "sha256-818ee5FXRg1JO8Z7hj/VyqEQ5zbfuo6hgOkTC1meh4A=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.ServicePoint.dll",
        "name": "System.Net.ServicePoint.76c86pbbv1.dll",
        "hash": "sha256-YdawtSUfe3Puu5an+8XM/d58CNemPhNIzsODx07bpEU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Sockets.dll",
        "name": "System.Net.Sockets.spmw5upbgz.dll",
        "hash": "sha256-ABj4lexI+RPdLp6z8K2QDazVn04LGQS2U4Qp/agvIgU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.WebClient.dll",
        "name": "System.Net.WebClient.oqxsf8q46q.dll",
        "hash": "sha256-0Mjo0YM1Jtp8+RIN/q7piiuMGaC75LH8x6N0OUHTVlQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.WebHeaderCollection.dll",
        "name": "System.Net.WebHeaderCollection.fujc7qih3j.dll",
        "hash": "sha256-d17BKc/CaBo2SdT/6frkIaXfMQYxrwnk6o2xRZNrtsw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.WebProxy.dll",
        "name": "System.Net.WebProxy.5d00gmh3yx.dll",
        "hash": "sha256-JwbbqD5X4IbC0c2QT8vbu/qxAbwoqxcgqVJ4BIwIXRk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.WebSockets.Client.dll",
        "name": "System.Net.WebSockets.Client.angxm5ef52.dll",
        "hash": "sha256-hnx20C2lEIQilvcl8f54yUX8uA+rvx6B2cpIwbZo05M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.WebSockets.dll",
        "name": "System.Net.WebSockets.fvnqtrq74d.dll",
        "hash": "sha256-pRsTCqmTaf3VyKaPc0E4dTCiIGaqBFgl6BkO+GGwCQ8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.dll",
        "name": "System.Net.ucsgl738qt.dll",
        "hash": "sha256-jtT9ERVwegJ2FWIfku6LSzNlga6n7MX/FjAJ7B0AmYI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Numerics.dll",
        "name": "System.Numerics.7le3wu1816.dll",
        "hash": "sha256-ZSuQatVy+7HApdBPWecEE8oSLiijodIfCqMeNWuvNwM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Numerics.Vectors.dll",
        "name": "System.Numerics.Vectors.6khw88yfre.dll",
        "hash": "sha256-5ijCx9EkMrZ1vAWw+v9UvGPvY54b82QCaoHrgntcRWc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ObjectModel.dll",
        "name": "System.ObjectModel.32n5thctmn.dll",
        "hash": "sha256-+yRki2BiMlIMcQNd2HYZmpaHx6JIQ8N+L53UUZRmJ/s=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.CoreLib.dll",
        "name": "System.Private.CoreLib.e845euk8da.dll",
        "hash": "sha256-UhVCiMGb1PmUnWu8dn+1w2E6U1pItMKenaCAOmJExxE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.DataContractSerialization.dll",
        "name": "System.Private.DataContractSerialization.6xub4lqhqu.dll",
        "hash": "sha256-Skk6BD3BlJQtYTWWJOjA9k7oJreujh2j2w3M6hQzIaA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.Uri.dll",
        "name": "System.Private.Uri.1ibgdo48o1.dll",
        "hash": "sha256-OWsiwMEhpJh9ghFamUx48X4oWOoNBEycmiQy9j4YLtc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.Xml.Linq.dll",
        "name": "System.Private.Xml.Linq.pa4nlusgn7.dll",
        "hash": "sha256-gaKmg1Y7L8U7cUNEn4F/mIndCskdaThnl977wREscgU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.Xml.dll",
        "name": "System.Private.Xml.e7rqvz79y9.dll",
        "hash": "sha256-xlhOuRYTaqr2/fh98GgxG+0bkD+AQ8BT1IObQ4u/8Qo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.dll",
        "name": "System.Reflection.1d7lincemt.dll",
        "hash": "sha256-hWYHM0AQFex2pqwVgKYXLNOaWgvY30db7g0+j0LmcAs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.DispatchProxy.dll",
        "name": "System.Reflection.DispatchProxy.x4ds0p8y53.dll",
        "hash": "sha256-gTWMnRasQakLHlRY+8cAsjWAp0TxsfglLSjnME7O174=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Emit.ILGeneration.dll",
        "name": "System.Reflection.Emit.ILGeneration.mcexe6xvat.dll",
        "hash": "sha256-GsP4K5prf4rtFp1+9iHtiY39IAx+akb4I7oSOkwbpMI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Emit.Lightweight.dll",
        "name": "System.Reflection.Emit.Lightweight.3mb5hqxky4.dll",
        "hash": "sha256-9czT7CseQQHfh86At636Nv6j0hgr71A/C/uIbdv8zF0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Emit.dll",
        "name": "System.Reflection.Emit.jl0namz9da.dll",
        "hash": "sha256-xzT/zAWSeLNQWeVNGHvfL6pY0eACHZwLRV7irOF4nUA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Extensions.dll",
        "name": "System.Reflection.Extensions.524k3hrhuu.dll",
        "hash": "sha256-c/dAcCYGvh2PR4Iba5SUnbzvCR+p70NiRDIYb0ftSQ8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Metadata.dll",
        "name": "System.Reflection.Metadata.rfp4y37e0g.dll",
        "hash": "sha256-x5erQ7Peh1NQGXq80olHSw60wY+fltS9oB0eql72+24=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Primitives.dll",
        "name": "System.Reflection.Primitives.6yzhneyiqs.dll",
        "hash": "sha256-wiB2IQTvn0fbNHCwzcpqAX1JpdrzAngrwhSXjR6Q1oA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.TypeExtensions.dll",
        "name": "System.Reflection.TypeExtensions.iljtujiymk.dll",
        "hash": "sha256-drLpzXon6HIuzugZhyP2p1I3ibqqI0qIQXnmsEes+/E=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Resources.Reader.dll",
        "name": "System.Resources.Reader.s8855stqc6.dll",
        "hash": "sha256-BOI2ZWQ4V6+7meqvKayGApoqcP6jRAccRaVwxmY8S5U=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Resources.ResourceManager.dll",
        "name": "System.Resources.ResourceManager.fapl3x35g4.dll",
        "hash": "sha256-uRXg6oTDTsrRYfP++gUxF1uMyHYWKqK+2k4Ns7jxIwQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Resources.Writer.dll",
        "name": "System.Resources.Writer.v1ubfu5jwp.dll",
        "hash": "sha256-4yNmvfX1HbdmQAJb4vw94hT1z0dZUqDs5Ke0cCWMA64=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.CompilerServices.Unsafe.dll",
        "name": "System.Runtime.CompilerServices.Unsafe.o53cwc19zw.dll",
        "hash": "sha256-BEWBrkWbPRy8GWdBMSkGx36tnWPpDvuwiufFwIhTWYQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.CompilerServices.VisualC.dll",
        "name": "System.Runtime.CompilerServices.VisualC.gi6gmq3phq.dll",
        "hash": "sha256-CI0BirNgM9zqe0g84DFW7C/qJc8NTEYUp0GHrGSElnQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Extensions.dll",
        "name": "System.Runtime.Extensions.825d6hq8et.dll",
        "hash": "sha256-4KSjC6DODto5uuQBsQ5TRTuyM1pADJYIZ3YinKz2+u8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Handles.dll",
        "name": "System.Runtime.Handles.hd237vho3v.dll",
        "hash": "sha256-xYEkbulTXPU+3Nrz2K40DuaDHyAXKk8UUd+rM+TooAk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.InteropServices.JavaScript.dll",
        "name": "System.Runtime.InteropServices.JavaScript.4j6xqfjk02.dll",
        "hash": "sha256-UMUU1IjqLKROoCEJ2swDfix9dLT1qIB32lu1N2f2yAA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.InteropServices.RuntimeInformation.dll",
        "name": "System.Runtime.InteropServices.RuntimeInformation.44463mt9pb.dll",
        "hash": "sha256-VGVlPSaaC51iWmqNlO5S5bp1WAhRO70VgOgqyBVmmk8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.InteropServices.dll",
        "name": "System.Runtime.InteropServices.hqk4xx6hw4.dll",
        "hash": "sha256-+cY8NfDXTLY0mmkW3u87wp2WNHudYw0wY43E+3uWGc4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Intrinsics.dll",
        "name": "System.Runtime.Intrinsics.4odt2z0tu5.dll",
        "hash": "sha256-dNibQr3+hhsjH1RWE1dsnYYBJSivDuS0+r+6M1W3EA8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Loader.dll",
        "name": "System.Runtime.Loader.hogrxjvbfg.dll",
        "hash": "sha256-MSo6Leg2SgFxzXGC6deR2gRDfsaQLEvi2Aan5olqNP8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Numerics.dll",
        "name": "System.Runtime.Numerics.ive6xb1afi.dll",
        "hash": "sha256-zu07GJABQcErP+x3KcC0N3SkGecVvNGo5ZkEhOo5Swc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.Formatters.dll",
        "name": "System.Runtime.Serialization.Formatters.kfsibbqmqc.dll",
        "hash": "sha256-kIg4lwvxKOLd66poRYwOSwlWKgu3UFsnh2+SOo/iVX0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.Json.dll",
        "name": "System.Runtime.Serialization.Json.rvyub7gqoo.dll",
        "hash": "sha256-6QzqmLMb77CfIyDLNTk7ZL5YOEzp8AnpY+RqaXG8W8A=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.Primitives.dll",
        "name": "System.Runtime.Serialization.Primitives.4k93r4m3ju.dll",
        "hash": "sha256-3AG+fuy2uLrfd4KZ48mlloh2EQZQk6Kt70DsPK1qDXQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.Xml.dll",
        "name": "System.Runtime.Serialization.Xml.6f9mn5yoen.dll",
        "hash": "sha256-zlsNgT4VLdXKCRKpoRdrb9IYZ+fB0M2LWKstJaGRkH0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.dll",
        "name": "System.Runtime.Serialization.zp4iyyk0l8.dll",
        "hash": "sha256-Z1KwNUOezrkit6nSYawmSlME6KPUWv1ZIQ16L0ONJHM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.dll",
        "name": "System.Runtime.ht62veqyr9.dll",
        "hash": "sha256-+9mTUJZrSVbfYDdY7Ld0GHbaQT0aq0H/pROnsUwejH4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.AccessControl.dll",
        "name": "System.Security.AccessControl.ljxeoxty36.dll",
        "hash": "sha256-r9La3YNw1PzhUtYRwWBBeh/qIV+wg1LDMsnPWgkgR28=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Claims.dll",
        "name": "System.Security.Claims.39qgvgr1ic.dll",
        "hash": "sha256-GWgIjcFbFWn/OdXyQFSE7rbxZylc/UiH/E6vBKz+meo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.dll",
        "name": "System.Security.Cryptography.34046axpng.dll",
        "hash": "sha256-bag8NhPCOJzW8gI61VHxEFgJ3FCdtSQCJHWYrVbdl2c=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.Algorithms.dll",
        "name": "System.Security.Cryptography.Algorithms.urh9nebx6l.dll",
        "hash": "sha256-OiEfvxVdsIxz93DP+b8+bWALosGXWWhllfQGYVByceY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.Cng.dll",
        "name": "System.Security.Cryptography.Cng.7vju417tzv.dll",
        "hash": "sha256-7WeDHlWGtjfkxkEpg8EQDctX4ygAR0RJYVR7vTXXU84=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.Csp.dll",
        "name": "System.Security.Cryptography.Csp.s088tu4qiw.dll",
        "hash": "sha256-nA/dqxqRnLU6ZTk4qx+CzU5b7dT9Nox21k3ULM+tImU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.Encoding.dll",
        "name": "System.Security.Cryptography.Encoding.vu4b6ctfi7.dll",
        "hash": "sha256-qQy2TekpniXGDM3qMDVTXenOft1Qax2yFOTLuqsrXBo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.OpenSsl.dll",
        "name": "System.Security.Cryptography.OpenSsl.absu8fmv3l.dll",
        "hash": "sha256-1q6OdTEB4cxC9ppCLfyFEh24PtDG6OGIFdsxTKR+jWA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.Primitives.dll",
        "name": "System.Security.Cryptography.Primitives.4p2j2ycw9l.dll",
        "hash": "sha256-aF6E6EBhb/FvsHTrxiYkc5YfVZ1wt4lXc9ZEGAG+OsE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.X509Certificates.dll",
        "name": "System.Security.Cryptography.X509Certificates.nl90dary7b.dll",
        "hash": "sha256-ZQpKdJNRon6UD9S8CToUfQ15knGlDkZ2dmeBVn1Ph0A=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Permissions.dll",
        "name": "System.Security.Permissions.mqq9smydvr.dll",
        "hash": "sha256-ngMjC8PQL5wEJC4UuWFGvz+63Wl6fIM6OiLNN/ze/Tc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Principal.dll",
        "name": "System.Security.Principal.9jmwtdezzf.dll",
        "hash": "sha256-65RS3Dw1kk/0g83Vdwn54CfePSsigwQwcxT96/tPUv8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Principal.Windows.dll",
        "name": "System.Security.Principal.Windows.yfq49p3dvu.dll",
        "hash": "sha256-IoR+9db7TXWIjgITWIsLuBQLiPuGjNTXViIQXnTeLw4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.SecureString.dll",
        "name": "System.Security.SecureString.yhwpdo609q.dll",
        "hash": "sha256-OqI1Dy1FA4vgf+aQwSMjs7EmGOq/btO+57GCY7XB+es=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.dll",
        "name": "System.Security.w4fgh6uky4.dll",
        "hash": "sha256-oBlyfLv3enhw1cgNhE92X2gel/GkHexnqIysJaKdTxY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ServiceModel.Web.dll",
        "name": "System.ServiceModel.Web.qm78iffip7.dll",
        "hash": "sha256-ojZ6QsxtuLE6mmlX720cZF+rRX31WGsy4mnTmDI1g5c=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ServiceProcess.dll",
        "name": "System.ServiceProcess.wfis5ba1lv.dll",
        "hash": "sha256-XF3fftw1DjYbRZ28GfLfvMvysQtCgXged6SQbHnDSJY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encoding.dll",
        "name": "System.Text.Encoding.9lg1dp3ud7.dll",
        "hash": "sha256-wxS2k6YFV0fHY68eYMvBgkVn0lhA33i/Mu/dej/O5iw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encoding.CodePages.dll",
        "name": "System.Text.Encoding.CodePages.hy0uk0kllk.dll",
        "hash": "sha256-p4ssl2r3oIOx09wT7DbvUP7Yr2o2NTt2h+YX1tqkZFY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encoding.Extensions.dll",
        "name": "System.Text.Encoding.Extensions.9j2696qi0d.dll",
        "hash": "sha256-qwy5cD+apZ/DYztozkaRwNv8xA/Ht+4DN/i4Dbfo8oQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encodings.Web.dll",
        "name": "System.Text.Encodings.Web.8lxnec1x6g.dll",
        "hash": "sha256-NCg9EAeMYcGDFU5wYgI1X6oBfs7KPhe03DWsudXsFlk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Json.dll",
        "name": "System.Text.Json.l1w2wkwb93.dll",
        "hash": "sha256-ub5e+16XkzVQQ8uJG3oFLC3Q7cbOoDtyryc/Vz2AQbM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.RegularExpressions.dll",
        "name": "System.Text.RegularExpressions.d2aac5nyck.dll",
        "hash": "sha256-i/4UNuMYFr3rXiL4CRuViVVbHya1RJzyCGGBtvdr7C0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.AccessControl.dll",
        "name": "System.Threading.AccessControl.inohvxbndc.dll",
        "hash": "sha256-DqrQ5myrEPYrUQp3U6mTjBplqBwj4SFTtIJizreKk/g=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Channels.dll",
        "name": "System.Threading.Channels.pgoblfnh9i.dll",
        "hash": "sha256-mdEPHTJeWWJi4BNmAN3linHKu2mCB0SBh7PXJhWd2zY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Overlapped.dll",
        "name": "System.Threading.Overlapped.gv30rui37l.dll",
        "hash": "sha256-ZNnHwTlO1fOe7v+oAM+1CMNE8ygzQ6mFy1dDRqSUATA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Tasks.Dataflow.dll",
        "name": "System.Threading.Tasks.Dataflow.2lvg3cjp8z.dll",
        "hash": "sha256-5gafcvU5S2t3DN6art282OqZUlGVrvG82BypbCrIHMw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Tasks.Extensions.dll",
        "name": "System.Threading.Tasks.Extensions.ot28pb0qan.dll",
        "hash": "sha256-TAXEpY0SPgRxGPOq0SXts7b3cdholpsDtD3d6GI2+VU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Tasks.Parallel.dll",
        "name": "System.Threading.Tasks.Parallel.ol5lk618o9.dll",
        "hash": "sha256-nIsc2/mm7YBGO1L+lLSdTxnW1IuBeAtIF4+WkWqMH4c=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Tasks.dll",
        "name": "System.Threading.Tasks.supvkprl38.dll",
        "hash": "sha256-XOOJWBx4zka+E3UZXGyKsc6vxw0FsS3iBQGiG6IMULg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Thread.dll",
        "name": "System.Threading.Thread.bfi4f0qq9q.dll",
        "hash": "sha256-uTqhssEDNgC7AteOV5UBjpbBGM8jMLljtzEJZIjCKyk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.ThreadPool.dll",
        "name": "System.Threading.ThreadPool.gcql2sbd7j.dll",
        "hash": "sha256-YGXB8JeNvpqExtwkBpRWJHU/V++Ytwuz1FhpfD05UDk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Timer.dll",
        "name": "System.Threading.Timer.04lynnasg7.dll",
        "hash": "sha256-YHLcBcU6nGVMuWirhaM8SLBmVMKpO3QYX8OquWwa/Zo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.dll",
        "name": "System.Threading.ao1mlyluko.dll",
        "hash": "sha256-BoFAEMATblHBz3gMqsGQ8HaGnW86KdxZvs9wRnjLHLI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Transactions.Local.dll",
        "name": "System.Transactions.Local.piuslsihv8.dll",
        "hash": "sha256-f/EGqALNboXuygwoCT70cfNfJMVOy+W0UMxC2Fpdha0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Transactions.dll",
        "name": "System.Transactions.wjf1nmc05a.dll",
        "hash": "sha256-BKyFzKD9MHWu6RXftJrJUDsB7TgJpJSlHI2dj1otLbs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ValueTuple.dll",
        "name": "System.ValueTuple.hsssdbip0q.dll",
        "hash": "sha256-P+YNKYVSNySBB8YhhEvwL+CEApBkXA9HI4UPD33yhmw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Web.HttpUtility.dll",
        "name": "System.Web.HttpUtility.vesfxw47t0.dll",
        "hash": "sha256-l34ahU84vgclr5ctuVKxAXZwZGSHye/Ib3fCyCYMljY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Web.dll",
        "name": "System.Web.m4cldiuunn.dll",
        "hash": "sha256-pkXeTU0j0zpqEy8fXFhDG9gebMV0IGKxsvwhPQYLKeU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Windows.Extensions.dll",
        "name": "System.Windows.Extensions.njtedi05bp.dll",
        "hash": "sha256-7X0SnvxN5QmsmPAodUpnn4MhVI7SOMvTmoz4XA+2U10=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Windows.dll",
        "name": "System.Windows.wql558q9uj.dll",
        "hash": "sha256-WCIL7/6xYeIKyxYIS6hNw4sWp4yjx/Q1ZFWGmL/Ko+s=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.Linq.dll",
        "name": "System.Xml.Linq.aq051b8tym.dll",
        "hash": "sha256-8v/h0iDQo+wmMG82QEGmdNyu/ZIoDEB65D2YEXxvgBY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.ReaderWriter.dll",
        "name": "System.Xml.ReaderWriter.139oq3z9po.dll",
        "hash": "sha256-Awb8XsEWuIrdgq67tagptoQ2nh/ccMBczqXjQylFTpE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.Serialization.dll",
        "name": "System.Xml.Serialization.nh0fn6k1lj.dll",
        "hash": "sha256-O3/kGx3FzLMF8nwoH2+3tVJ9zva/8XcGp5RTWSFu8vM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.XDocument.dll",
        "name": "System.Xml.XDocument.heiyjmfb61.dll",
        "hash": "sha256-qUxSkd0cSc9UUerTVIfmGobNxAyiCsWIOIZgBypYLgo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.XPath.XDocument.dll",
        "name": "System.Xml.XPath.XDocument.6t1sd04uri.dll",
        "hash": "sha256-KuU73P9LHvJC7/bf3LstE0K3MupP4Quyep5fabo0PnA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.XPath.dll",
        "name": "System.Xml.XPath.aqhettx5q6.dll",
        "hash": "sha256-QnLgPBfhcWRRVGwMHKzR1l6+Kwb5UkGX+TcQlfozhb0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.XmlDocument.dll",
        "name": "System.Xml.XmlDocument.5qpivw1no1.dll",
        "hash": "sha256-vSHPjOy8KnRRIuadPm+/5Wa4Pk/qBKPie2VZTRkCqNI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.XmlSerializer.dll",
        "name": "System.Xml.XmlSerializer.4frh8uemtb.dll",
        "hash": "sha256-8LqxOJxd3KqakQjptqiM1SkwSrGkgoXod4VQvqdpS74=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.dll",
        "name": "System.Xml.xf18cpcjdc.dll",
        "hash": "sha256-szb8DhlUqS+n6yDjVJrcqu9aWnEQ0nH6Hxey7E4nvKA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.dll",
        "name": "System.mg89w5jal8.dll",
        "hash": "sha256-6mhOaxkG3Nbakjzb4t2yVVy2gBweHP5GV1Gf8yHOmh0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "WindowsBase.dll",
        "name": "WindowsBase.fnezy6om57.dll",
        "hash": "sha256-627EAMhkqFsrBMLPAwMHh08aWgt5fDlDq8EhyUNQQmA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "YamlDotNet.dll",
        "name": "YamlDotNet.ke5e9gdd1g.dll",
        "hash": "sha256-JPQWkWv92bjGGzblnelKP9RIDHKbO66FHqv5+LVHjd4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "mscorlib.dll",
        "name": "mscorlib.tiu6stdi3r.dll",
        "hash": "sha256-+5/VeEWjDdn8OrdMFYoKZgFGZ/EhhVlXJ9+fJfA8uew=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "netstandard.dll",
        "name": "netstandard.92xukzzkul.dll",
        "hash": "sha256-H80G7995AJitbvccQXK6tNbfdAImQFGrzpsmfCEkDr0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "protobuf-net.Core.dll",
        "name": "protobuf-net.Core.d67i8e86zb.dll",
        "hash": "sha256-Kwv0Si5a1SiClrt53H9a2KsyJNPlUGQUkd2W1AoQdXY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "protobuf-net.dll",
        "name": "protobuf-net.r5091oifyi.dll",
        "hash": "sha256-8bVx/6Q0JUG3+5gD/ycW5x6wPcLz34VNBIAoZWniWu0=",
        "cache": "force-cache"
      }
    ],
    "assembly": []
  },
  "debugLevel": 0,
  "globalizationMode": "sharded",
  "runtimeConfig": {
    "runtimeOptions": {
      "configProperties": {
        "System.Diagnostics.Debugger.IsSupported": false,
        "System.Diagnostics.Metrics.Meter.IsSupported": false,
        "System.Diagnostics.Tracing.EventSource.IsSupported": false,
        "System.Globalization.Invariant": false,
        "System.TimeZoneInfo.Invariant": false,
        "System.Linq.Enumerable.IsSizeOptimized": true,
        "System.Net.Http.EnableActivityPropagation": false,
        "System.Net.Http.WasmEnableStreamingResponse": true,
        "System.Net.SocketsHttpHandler.Http3Support": false,
        "System.Reflection.Metadata.MetadataUpdater.IsSupported": false,
        "System.Resources.UseSystemResourceKeys": true,
        "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": true,
        "System.Text.Encoding.EnableUnsafeUTF7Encoding": false
      }
    }
  }
}/*json-end*/);export{ht as default,gt as dotnet,pt as exit};

