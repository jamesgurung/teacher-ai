const urlRegex = /(https?:\/\/)?(([a-z0-9][a-z0-9-]*[a-z0-9]\.)+[a-z]{2,})(\:\d+)?(\/[-a-z0-9_:@&?=+,.!/~*%$]*)?/i;
const $messages = document.querySelector('#messages');
const $prompt = document.querySelector('#prompt');
const $main = document.querySelector('main');
const $header = document.querySelector('header');
const $credits = document.querySelector('#credits');
const chat = [];
const conversationId = crypto.randomUUID();
let $feedbackStatus;
let $feedbackProgressBar;
let $response;
let incomingMessage;
let typing = false;
let template = null;
let templateStage = 1;
let templateId = 'open';
let currentTemplates = templates;
let temperature = 0.4;
let adminMode = false;
let feedbackMode = false;
let optionsShown = true;

const connection = new signalR.HubConnectionBuilder().withUrl('/chat').withAutomaticReconnect().build();
connection.start();
document.addEventListener('visibilitychange', async () => {
  if (document.visibilityState === 'visible' && connection.state === signalR.HubConnectionState.Disconnected) await connection.start();
});

marked.setOptions({ breaks: true, gfm: true, headerIds: false, mangle: false });

if (credits > 0) {
  displayOptions();
} else {
  $messages.innerHTML = '';
}
updateCreditsUI();

$messages.addEventListener('click', async function (e) {
  if (e.target.classList.contains('option') || e.target.parentNode.classList.contains('option'))
  {
    template = currentTemplates.find(o => o.id === (e.target.dataset.id ?? e.target.parentNode.dataset.id));
    [...$messages.getElementsByClassName('option'), ...$messages.getElementsByClassName('initial-option')].forEach(o => o.remove());
    optionsShown = false;

    if (template.templates) {
      currentTemplates = template.templates;
      $prompt.disabled = true;
      displayOptions();
      return;
    }

    template.userInputs = [];
    const userBubble = document.createElement('div');
    userBubble.className = 'message user title' + (template.admin ? ' admin' : '');
    userBubble.innerHTML = `<p>${template.title}</p>`;
    $messages.appendChild(userBubble);

    const assistantBubble = document.createElement('div');
    assistantBubble.className = 'message assistant';
    assistantBubble.innerHTML = marked.parse(template.messages[0].text.replace(/SERVICE_ACCOUNT/g, serviceAccountEmail));
    if (template.messages[0].hint) {
      const hint = document.createElement('span');
      hint.textContent = template.messages[0].hint;
      assistantBubble.firstElementChild.appendChild(hint);
    }
    $messages.appendChild(assistantBubble);

    $prompt.disabled = false;
    $prompt.focus();
  }
  else if (e.target.className === 'boost') {
    chat.pop();
    await send(true);
  }
});

(window.visualViewport ?? window).onresize = onResize;
function onResize() {
  const viewportHeight = window.visualViewport?.height ?? window.innerHeight;
  $main.style.height = (viewportHeight - $header.clientHeight + 15) + 'px';
  $messages.scrollTop = $messages.scrollHeight;
};
onResize();

$prompt.addEventListener('input', adjustHeight);
function adjustHeight() {
  const maxLines = 6;
  $prompt.style.height = '28px';

  if (($prompt.scrollHeight - 12) / 16 <= maxLines) {
    $prompt.style.height = ($prompt.scrollHeight + 2) + 'px';
  } else {
    $prompt.style.height = '110px';
  }
  $messages.style.bottom = ($prompt.offsetHeight + 20) + 'px';
}
adjustHeight();

$prompt.addEventListener('keypress', async function (e) {
  const key = e.which || e.keyCode;
  if (optionsShown) {
    [...$messages.getElementsByClassName('option'), ...$messages.getElementsByClassName('initial-option')].forEach(o => o.remove());
    optionsShown = false;
  }
  if (key === 13 && !e.shiftKey && $prompt.value) {
    await send();
    e.preventDefault();
    return false;
  }
});

async function send(boost) {
  let message = removeWhitespace($prompt.value);
  if (!boost && message.length === 0) return;

  if ((!template || !template.feedbackMode) && urlRegex.test(message)) {
    alert('The AI Assistant is unable to follow links. Please paste the contents of the web page.');
    return;
  }

  let modelType = ((credits / maxCredits) > 0.2 || boost) ? 'default' : 'small';
  let showFollowUpHint = false;

  [...document.querySelectorAll('.boost,.hint')].forEach(o => o.remove());

  const userBubble = document.createElement('div');
  userBubble.className = 'message user';
  const userBubbleParagraph = document.createElement('p');
  userBubbleParagraph.innerHTML = boost ? '&#x1F680; <i>Regenerate with more powerful AI</i>' : marked.parse(message);
  userBubble.appendChild(userBubbleParagraph);
  $messages.appendChild(userBubble);

  if (!boost) $prompt.value = '';

  adjustHeight();

  if (template) {
    template.userInputs.push(message);
  }

  if (template && templateStage < template.messages.length) {

    const assistantBubble = document.createElement('div');
    assistantBubble.className = 'message assistant';
    assistantBubble.innerHTML = marked.parse(template.messages[templateStage].text);

    if (template.messages[templateStage].hint) {
      const hint = document.createElement('span');
      hint.textContent = template.messages[templateStage].hint;
      assistantBubble.firstElementChild.appendChild(hint);
    }
    $messages.appendChild(assistantBubble);
    $messages.scrollTop = $messages.scrollHeight;
    templateStage++;

  } else {

    $prompt.disabled = true;

    if (template) {
      message = template.prompt?.replace(/\[([0-9]+)\]/g, (_, index) => template.userInputs[index]) ?? message;
      temperature = template.temperature;
      templateId = template.id;
      if (template.admin) adminMode = true;
      if (template.feedbackMode) feedbackMode = true;
      template = null;
      showFollowUpHint = true;
    }

    if (feedbackMode) {
      $response = document.createElement('div');
      $response.className = 'message assistant full-width' + (modelType === 'default' ? ' boosted' : '');
      $response.innerHTML = '<p class="status">Opening spreadsheet...</p><p><div class="progress"><div></div></div></p>'
      $messages.appendChild($response);
      $messages.scrollTop = $messages.scrollHeight;
      $feedbackProgressBar = document.getElementsByClassName('progress')[0].firstElementChild;
      $feedbackStatus = document.getElementsByClassName('status')[0];

      const resp = await request('/api/feedback', 'POST', {
        url: message,
        conversationId: conversationId,
        connectionId: connection.connectionId
      }, false);

      if (!resp.ok) {
        const problem = await resp.json();
        const $issue = document.createElement('p');
        $issue.className = 'issue';
        $issue.textContent = problem.detail;
        $response.appendChild($issue);
        $feedbackStatus.textContent = 'An error occurred. Please start over.';
        $feedbackProgressBar.style.backgroundColor = 'red';
        $response.classList.add('error');
        $messages.scrollTop = $messages.scrollHeight;
      } else {
        $feedbackProgressBar.style.width = '100%';
        $feedbackStatus.innerHTML = 'Marking complete. You may need to refresh the spreadsheet.';
      }
      return;
    }

    if (!boost) {
      chat.push({ role: 'user', content: message });
    }

    $response = document.createElement('div');
    $response.className = 'message assistant' + (modelType === 'default' ? ' boosted' : '');
    $response.innerHTML = '<div class="typing"><span></span><span></span><span></span></div>'
    $messages.appendChild($response);
    $messages.scrollTop = $messages.scrollHeight;

    typing = true;
    incomingMessage = '';

    if (adminMode) {
      userBubble.classList.add('admin');
      const resp = await request('/api/admin', 'POST', {
        command: message
      });
      const output = await resp.json();
      $response.innerHTML = marked.parse(output);
      $prompt.disabled = false;
      $prompt.focus();
      return;
    }

    const resp = await request('/api/chat', 'POST', {
      messages: chat,
      temperature: temperature,
      modelType: modelType,
      templateId: templateId,
      conversationId: conversationId,
      connectionId: connection.connectionId
    });
    typing = false;
    const data = await resp.json();
    $response.innerHTML = marked.parse(data.response);
    chat.push({ role: 'assistant', content: data.response });
    if (data.finishReason !== 'stop') {
      const $issue = document.createElement('p');
      $issue.className = 'issue';
      switch (data.finishReason) {
        case 'length':
          $issue.textContent = 'Sorry, this response has been cut short because the maximum conversation length was reached.';
          break;
        case 'content_filter':
        case 'prompt_filter':
          $issue.textContent = 'Sorry, the content filter was triggered and this conversation cannot continue. The content filter is designed to prevent abuse, but unfortunately some educational topics are incorrectly flagged. Avoid asking the AI about sensitive topics, such as literature or history involving violence or suffering, or controversial religious or ethical themes.';
          break;
        default:
          $issue.textContent = 'Sorry, the AI service encountered an error.';
          break;
      }
      $response.appendChild($issue);
    }
    if (modelType === 'small' && data.remainingCredits > 0 && data.finishReason === 'stop') {
      const $boost = document.createElement('div');
      $boost.className = 'boost';
      $boost.title = 'Regenerate with more powerful AI';
      $boost.innerHTML = '&#x1F680;';
      $response.appendChild($boost);
    }
    credits = data.remainingCredits;
    updateCreditsUI();
    if (credits > 0 && data.finishReason === 'stop') {
      if (chat.filter(o => o.role === 'user').length < 11 && !data.stop) {
        if (showFollowUpHint) {
          const $followUpHint = document.createElement('div');
          $followUpHint.className = 'hint';
          $followUpHint.innerHTML = 'Ask follow-up questions below, or <a href="/">start a new chat</a>.';
          $messages.appendChild($followUpHint);
        }
        $prompt.disabled = false;
        $prompt.focus();
      } else {
        $prompt.value = 'You have reached the maximum number of messages. Click "New Chat" to start a new conversation.';
      }
    }
    $messages.scrollTop = $messages.scrollHeight;
  }
}
$prompt.focus();

connection.on('Type', function (token) {
  if (!typing) return;
  if ($response.getElementsByClassName('typing').length > 0) $response.innerHTML = '';
  incomingMessage += token;
  $response.innerHTML = marked.parse(incomingMessage);
  $messages.scrollTop = $messages.scrollHeight;
});

connection.on('Feedback', function (student, index, total) {
  var perc = index == 0 ? Math.min((index + 1) / total * 50, 5) : (index / total * 100);
  $feedbackProgressBar.style.width = `${perc}%`;
  $feedbackStatus.textContent = `Writing feedback for ${student} (this may take a few minutes)...`;
});

async function request(url, method, body, alertError = true) {
  const headers = { 'X-XSRF-TOKEN': antiforgeryToken };
  if (body) headers['Content-Type'] = 'application/json';
  const resp = await fetch(url, {
    method: method,
    headers: headers,
    body: body ? JSON.stringify(body) : null
  });
  if (!resp.ok && alertError) {
    const text = await resp.text();
    alert(text ? JSON.parse(text) : 'An error occurred.');
  }
  return resp;
}

function updateCreditsUI() {
  $credits.title = `${credits} credits remaining this week`;
  var $bar = $credits.firstElementChild;
  var perc = Math.min(100, credits / maxCredits * 100);
  $bar.style.width = `${perc}%`;
  $bar.className = perc <= 20 ? 'red' : 'green';
  if (credits <= 0) {
    [...document.getElementsByClassName('boost')].forEach(o => o.remove());
    $prompt.disabled = true;
    $prompt.value = '';

    $error = document.createElement('div');
    $error.className = 'message assistant error';
    $error.innerHTML = `<p>Sorry, you have run out of AI credits for now. Your balance resets on Monday next week.</p><p>If you are working on a task which adds significant value to our students or the organisation, please contact ${adminName} to request additional credits.`;
    $messages.appendChild($error);
    $messages.scrollTop = $messages.scrollHeight;
  }
}

function displayOptions() {
  for (const template of currentTemplates) {
    if (template.admin && !isAdmin) continue;
    const $template = document.createElement('div');
    $template.className = 'option' + (template.admin ? ' admin' : '');
    $template.innerHTML = `<p>${template.title}</p>`;
    $template.dataset.id = template.id;
    $messages.appendChild($template);
  }
  optionsShown = true;
}

function removeWhitespace(str) {
  const lines = str.split('\n');
  const resultLines = [];
  let emptyLinesCount = 0;
  for (const line of lines) {
    if (/^\s*$/.test(line)) {
      if (++emptyLinesCount <= 1) resultLines.push('');
    } else {
      emptyLinesCount = 0;
      resultLines.push(line.trimEnd());
    }
  }
  return resultLines.join('\n').trim();
}