let currentResponseText = '';
let currentResponseElement = null;
let searchStatusElement = null;
let textContainer = null;
let instanceId = null;

const connection = new signalR.HubConnectionBuilder().withUrl('/hub').withAutomaticReconnect().configureLogging(signalR.LogLevel.Warning).build();
connection.start();
document.addEventListener('visibilitychange', async () => {
  if (document.visibilityState === 'visible' && connection.state === signalR.HubConnectionState.Disconnected) await connection.start();
});

connection.on('Append', function (chunk, intendedInstanceId) {
  if (instanceId !== intendedInstanceId) return;

  if (!currentResponseElement) {
    removeTypingIndicator();
    currentResponseElement = document.createElement('div');
    currentResponseElement.className = 'message assistant-message';
    chatContentContainer.appendChild(currentResponseElement);
    textContainer = null;
  }

  switch (chunk) {
    case '[web_search_in_progress]':
    case '[file_search_in_progress]':
      const inProgressText = chunk === '[web_search_in_progress]' ? 'Searching the web...' : 'Searching documents...';
      searchStatusElement = document.createElement('div');
      searchStatusElement.className = 'search-container';
      currentResponseElement.appendChild(searchStatusElement);
      searchStatusElement.innerHTML = `<div class="search-in-progress"><span class="material-symbols-rounded">search</span> ${inProgressText}</div>`;
      break;
    case '[web_search_completed]':
    case '[file_search_completed]':
      const completedText = chunk === '[web_search_completed]' ? 'Searched the web.' : 'Searched documents.';
      searchStatusElement.innerHTML = `<div class="search-completed"><span class="material-symbols-rounded">check_circle</span> ${completedText}</div>`;
      textContainer = null;
      break;
    default:
      if (!textContainer) {
        textContainer = document.createElement('div');
        currentResponseElement.appendChild(textContainer);
        currentResponseText = '';
      }
      currentResponseText += chunk;

      const stopCommand = stopCommands.find(({ token }) => currentResponseText.includes(token));
      if (stopCommand) {
        showStopMessage(currentResponseElement, stopCommand);
      } else {
        textContainer.innerHTML = markdownToHtml(currentResponseText);
      }
      break;
  }

  scrollChatContainer();
});