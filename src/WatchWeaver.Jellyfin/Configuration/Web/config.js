(function () {
  const id = '5f36de72-9df2-4a06-b5e7-d55fe8f50158';
  const page = document.querySelector('#WatchWeaverConfigPage');
  let savedToken = '';

  function selectedUserIds() {
    return Array.from(page.querySelectorAll('#AllowedUsers input:checked')).map(input => input.value);
  }

  function renderUsers(users, selected) {
    const container = page.querySelector('#AllowedUsers');
    container.replaceChildren();
    users.forEach(user => {
      const label = document.createElement('label');
      label.className = 'checkboxContainer';
      const input = document.createElement('input');
      input.type = 'checkbox';
      input.value = user.Id;
      input.checked = selected.includes(user.Id);
      input.setAttribute('is', 'emby-checkbox');
      const name = document.createElement('span');
      name.textContent = user.Name || user.Id;
      label.append(input, name);
      container.append(label);
    });
    if (users.length === 0) container.textContent = 'No Jellyfin users were returned.';
  }

  page.addEventListener('pageshow', async () => {
    const [configuration, users] = await Promise.all([
      ApiClient.getPluginConfiguration(id),
      ApiClient.getUsers(),
    ]);
    page.querySelector('#WatchWeaverUrl').value = configuration.WatchWeaverUrl || '';
    page.querySelector('#ConnectionToken').value = '';
    savedToken = configuration.ConnectionToken || '';
    page.querySelector('#SavedTokenStatus').textContent = savedToken ? 'A token is saved.' : 'No token is saved yet.';
    renderUsers(users, configuration.AllowedUserIds || []);
  });

  page.querySelector('form').addEventListener('submit', async event => {
    event.preventDefault();
    Dashboard.showLoadingMsg();
    try {
      const configuration = await ApiClient.getPluginConfiguration(id);
      configuration.WatchWeaverUrl = page.querySelector('#WatchWeaverUrl').value.trim();
      const enteredToken = page.querySelector('#ConnectionToken').value.trim();
      if (enteredToken) {
        configuration.ConnectionToken = enteredToken;
        savedToken = enteredToken;
      }
      configuration.AllowedUserIds = selectedUserIds();
      const result = await ApiClient.updatePluginConfiguration(id, configuration);
      page.querySelector('#ConnectionToken').value = '';
      page.querySelector('#SavedTokenStatus').textContent = savedToken ? 'A token is saved.' : 'No token is saved yet.';
      Dashboard.processPluginConfigurationUpdateResult(result);
    } finally {
      Dashboard.hideLoadingMsg();
    }
  });

  page.querySelector('#TestConnection').addEventListener('click', async () => {
    const output = page.querySelector('#WatchWeaverStatus');
    const base = page.querySelector('#WatchWeaverUrl').value.trim().replace(/\/$/, '');
    const token = page.querySelector('#ConnectionToken').value.trim() || savedToken;
    if (!base || !token) {
      output.textContent = 'Enter the WatchWeaver URL and token first.';
      return;
    }
    output.textContent = 'Testing…';
    try {
      const response = await fetch(base + '/api/v1/ingest/jellyfin/events', {
        method: 'HEAD',
        headers: { Authorization: 'Bearer ' + token },
      });
      output.textContent = response.ok
        ? 'Connected · protocol v' + (response.headers.get('X-WatchWeaver-Protocol-Version') || '1')
        : 'Connection rejected (HTTP ' + response.status + ').';
    } catch (_) {
      output.textContent = 'Could not reach WatchWeaver. Check the URL and private network access.';
    }
  });
})();
