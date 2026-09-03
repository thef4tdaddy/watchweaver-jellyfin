(function(){
  const id='5f36de72-9df2-4a06-b5e7-d55fe8f50158';
  const page=document.querySelector('#WatchWeaverConfigPage');
  page.addEventListener('pageshow',()=>ApiClient.getPluginConfiguration(id).then(c=>{
    page.querySelector('#WatchWeaverUrl').value=c.WatchWeaverUrl||'';
    page.querySelector('#AllowedUserIds').value=(c.AllowedUserIds||[]).join(', ');
    page.querySelector('#ConnectionToken').value='';
  }));
  page.querySelector('form').addEventListener('submit',e=>{
    e.preventDefault();Dashboard.showLoadingMsg();
    ApiClient.getPluginConfiguration(id).then(c=>{
      c.WatchWeaverUrl=page.querySelector('#WatchWeaverUrl').value.trim();
      const token=page.querySelector('#ConnectionToken').value.trim();if(token)c.ConnectionToken=token;
      c.AllowedUserIds=page.querySelector('#AllowedUserIds').value.split(',').map(x=>x.trim()).filter(Boolean);
      return ApiClient.updatePluginConfiguration(id,c);
    }).then(Dashboard.processPluginConfigurationUpdateResult).finally(Dashboard.hideLoadingMsg);
  });
  page.querySelector('#TestConnection').addEventListener('click',async()=>{
    const out=page.querySelector('#WatchWeaverStatus');
    const base=page.querySelector('#WatchWeaverUrl').value.trim().replace(/\/$/,'');
    const token=page.querySelector('#ConnectionToken').value.trim();
    if(!base||!token){out.textContent='Enter the URL and token to test before saving.';return;}
    out.textContent='Testing…';
    try{const response=await fetch(base+'/api/v1/ingest/jellyfin/events',{method:'HEAD',headers:{Authorization:'Bearer '+token}});out.textContent=response.ok?'Connected · protocol v'+(response.headers.get('X-WatchWeaver-Protocol-Version')||'1'):'Connection rejected (HTTP '+response.status+').';}
    catch(_){out.textContent='Could not reach WatchWeaver. Check the URL, HTTPS, and network access.';}
  });
})();
