let tabs = [];
let activeTabId = null;
let expandedDirs = {};
let rootDir = '';
let isRunning = false;
let currentStreamController = null;

const fileTree = $('#fileTree');
const tabBar = $('#tabBar');
const tabContent = $('#tabContent');
const termOutput = $('#terminalOutput');
const AI_TAB_ID = 'ai-todolist';

// ====== Directory ======
function setDirectory() {
  const dir = $('#dirInput').val().trim();
  if (!dir) return;
  rootDir = dir;
  $.ajax({
    url: '/api/project/set-directory',
    method: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({ path: dir }),
    success: function () {
      $('#dirModal').removeClass('open');
      loadFileTree();
      addTab(AI_TAB_ID, 'AI Agent', true, false);
    },
    error: function (xhr) {
      alert('Error: ' + xhr.responseText);
    }
  });
}

// ====== File Tree ======
function loadFileTree(dir) {
  const params = dir ? '?dir=' + encodeURIComponent(dir) : '';
  $.get('/api/file/list' + params, function (data) {
    renderTree(data.items, dir || '');
  });
}

function renderTree(items, parentPath) {
  if (!parentPath) { fileTree.empty(); }
  const container = parentPath ? fileTree.find('.children[data-path="' + parentPath.replace(/"/g, '&quot;') + '"]') : fileTree;
  items.forEach(function (item) {
    const div = $('<div>').addClass('tree-item');
    const depth = parentPath ? parentPath.split(/[\/\\]/).length : 0;
    div.css('paddingLeft', (depth + 1) * 16);
    if (item.isDirectory) {
      const isOpen = !!expandedDirs[item.path];
      div.html('<span class="icon folder">' + (isOpen ? '&#x25BC;' : '&#x25B6;') + '</span>' + item.name);
      div.on('click', function (e) {
        e.stopPropagation();
        toggleDir(item.path);
      });
      container.append(div);
      const children = $('<div>').addClass('children' + (isOpen ? ' open' : '')).attr('data-path', item.path);
      container.append(children);
      if (isOpen) { loadFileTree(item.path); }
    } else {
      div.html('<span class="icon file">&#x2219;</span>' + item.name);
      div.on('click', function () { openFile(item.path); });
      container.append(div);
    }
  });
}

function toggleDir(path) {
  expandedDirs[path] = !expandedDirs[path];
  loadFileTree();
}

function refreshFileTree() {
  expandedDirs = {};
  loadFileTree();
}

function toggleSidebar() {
  const sb = $('#sidebar');
  sb.toggleClass('open');
  const btn = sb.find('.toggle-btn');
  btn.html(sb.hasClass('open') ? '&#x2715;' : '&#x2630;');
  if (sb.hasClass('open') && !fileTree.children().length) loadFileTree();
}

// ====== Tabs ======
function addTab(id, title, isPinned, switchTo) {
  const existing = tabs.find(function (t) { return t.id === id; });
  if (existing) {
    if (switchTo !== false) setActiveTab(id);
    return existing;
  }
  const tab = { id: id, title: title, isPinned: isPinned };
  tabs.push(tab);
  if (switchTo !== false) setActiveTab(id);
  renderTabs();
  if (!isPinned && id !== AI_TAB_ID) {
    loadFileContent(id);
  }
  return tab;
}

function closeTab(id) {
  const tab = tabs.find(function (t) { return t.id === id; });
  if (!tab || tab.isPinned) return;
  tabs = tabs.filter(function (t) { return t.id !== id; });
  tabContent.find('#' + escapeId(id)).remove();
  if (activeTabId === id) {
    setActiveTab(tabs.length > 0 ? tabs[tabs.length - 1].id : AI_TAB_ID);
  }
  renderTabs();
}

function setActiveTab(id) {
  activeTabId = id;
  renderTabs();
  $('.tab-pane').removeClass('active');
  const pane = $('#' + escapeId(id));
  if (pane.length) pane.addClass('active');
  showSaveButton(id !== AI_TAB_ID);
}

function showSaveButton(show) {
  let btn = $('#saveBtn');
  if (show && activeTabId !== AI_TAB_ID) {
    if (!btn.length) {
      btn = $('<button>').attr('id', 'saveBtn').addClass('save-btn').html('&#x1F4BE; Save');
      btn.on('click', function () { saveFile(activeTabId); });
      $('.tab-bar').append(btn);
    }
    btn.show();
  } else if (btn.length) {
    btn.hide();
  }
}

function saveFile(path) {
  const textarea = $('#' + escapeId(path)).find('.code-editor');
  if (!textarea.length) return;
  const content = textarea.val();
  $('#saveBtn').prop('disabled', true).text('Saving...');
  $.ajax({
    url: '/api/file/write',
    method: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({ path: path, content: content }),
    success: function () {
      $('#saveBtn').prop('disabled', false).html('&#x1F4BE; Save');
    },
    error: function () {
      $('#saveBtn').prop('disabled', false).html('&#x1F4BE; Save Failed');
    }
  });
}

function renderTabs() {
  tabBar.empty();
  tabs.forEach(function (tab) {
    const t = $('<div>').addClass('tab').addClass(tab.isPinned ? 'pinned-tab' : 'code-tab');
    if (tab.id === activeTabId) t.addClass('active');
    const icon = tab.isPinned ? '&#x2699;' : '&#x2219;';
    t.html('<span class="tab-icon">' + icon + '</span>' + tab.title +
      '<span class="tab-close' + (tab.isPinned ? ' disabled' : '') + '">&#x2715;</span>');
    t.on('click', function () { setActiveTab(tab.id); });
    t.find('.tab-close').on('click', function (e) { e.stopPropagation(); closeTab(tab.id); });
    tabBar.append(t);
  });
  const addBtn = $('<div>').addClass('tab-add').on('click', function () { ensureAITab(); });
  addBtn.html('+');
  tabBar.append(addBtn);
}

function escapeId(id) {
  return 'pane-' + id.replace(/[^a-zA-Z0-9_-]/g, '_');
}

function openFile(path) {
  addTab(path, path.split(/[\/\\]/).pop(), false);
}

function loadFileContent(path) {
  const id = path;
  if ($('#' + escapeId(id)).length > 0) return;
  const lang = getLanguageClass(path);
  const pane = $('<div>').addClass('tab-pane').attr('id', escapeId(id));
  const wrapper = $('<div>').addClass('editor-wrapper');
  const highlight = $('<pre>').addClass('editor-highlight');
  const code = $('<code>').addClass(lang).appendTo(highlight);
  const textarea = $('<textarea>').addClass('editor-textarea');
  textarea.attr('spellcheck', false).attr('data-path', path);
  textarea.on('keydown', function (e) {
    if (e.key === 's' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); saveFile(path); }
  });
  textarea.on('input', function () { syncHighlight(textarea, code, lang); });
  textarea.on('scroll', function () { highlight.scrollTop(textarea.scrollTop()); highlight.scrollLeft(textarea.scrollLeft()); });
  wrapper.append(highlight);
  wrapper.append(textarea);
  pane.append(wrapper);
  tabContent.append(pane);
  if (id === activeTabId) { pane.addClass('active'); showSaveButton(true); }
  $.get('/api/file/read', { path: path }, function (data) {
    const clean = data.content.replace(/^\s*\d+\|\s?/gm, '');
    textarea.val(clean);
    syncHighlight(textarea, code, lang);
  }).fail(function () {
    textarea.val('Error loading file: ' + path);
    syncHighlight(textarea, code, lang);
  });
}

function syncHighlight(textarea, code, lang) {
  const text = textarea.val();
  code.text(text);
  delete code[0].dataset.highlighted;
  hljs.highlightElement(code[0]);
}

function getLanguageClass(path) {
  const ext = path.split('.').pop().toLowerCase();
  const map = {
    cs: 'csharp', js: 'javascript', ts: 'typescript', html: 'xml',
    css: 'css', py: 'python', json: 'json', xml: 'xml',
    md: 'markdown', yaml: 'yaml', yml: 'yaml', sh: 'bash',
    ps1: 'powershell', bat: 'dos', cpp: 'cpp', c: 'c',
    java: 'java', go: 'go', rs: 'rust', rb: 'ruby',
    php: 'php', sql: 'sql', txt: 'plaintext', h: 'c',
    csproj: 'xml', sln: 'xml', config: 'xml'
  };
  return map[ext] || 'plaintext';
}

// ====== AI Agent (ToDoList) ======
function ensureAITab() {
  if (!tabs.find(function (t) { return t.id === AI_TAB_ID; })) {
    addTab(AI_TAB_ID, 'AI Agent', true);
  }
  setActiveTab(AI_TAB_ID);
}

function setupAITab() {
  const pane = $('<div>').addClass('tab-pane active').attr('id', escapeId(AI_TAB_ID));
  pane.html(`
    <div class="todolist-container">
      <div class="todolist-input-area">
        <div class="todolist-label">Project Requirements</div>
        <textarea id="reqInput" rows="3" placeholder="Describe your project... e.g. Create a Python Hello World project with main.py that prints Hello World"></textarea>
        <button id="runBtn" onclick="runToDoList()">&#x25B6; Run</button>
      </div>
      <div class="todolist-output" id="todolistOutput">
        <div class="empty-state">Enter requirements and click Run to start</div>
      </div>
    </div>
  `);
  tabContent.append(pane);
}

let abortRunning = false;

function runToDoList() {
  const desc = $('#reqInput').val().trim();
  if (!desc || isRunning) return;
  isRunning = true;
  abortRunning = false;
  $('#runBtn').prop('disabled', true).text('\u23F3 Running...');

  const out = $('#todolistOutput');
  out.empty();

  const planSection = $('<div>').addClass('todolist-section').appendTo(out);
  $('<div>').addClass('section-title').text('Plan').appendTo(planSection);
  const planList = $('<div>').addClass('plan-list').appendTo(planSection);

  const execSection = $('<div>').addClass('todolist-section').appendTo(out);
  $('<div>').addClass('section-title').text('Execution').appendTo(execSection);
  const execLog = $('<div>').addClass('exec-log').appendTo(execSection);

  let stepCount = 0;
  let stepEls = [];

  fetch('/api/todolist/run', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ description: desc })
  }).then(function (response) {
    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    function read() {
      reader.read().then(function (result) {
        if (result.done) {
          isRunning = false;
          $('#runBtn').prop('disabled', false).text('\u25B6 Run');
          return;
        }
        const text = decoder.decode(result.value, { stream: true });
        const lines = text.split('\n');
        lines.forEach(function (line) {
          if (line.startsWith('data: ')) {
            try {
              const data = JSON.parse(line.slice(6));
              handleEvent(data, planList, execLog);
            } catch (e) { }
          }
        });
        read();
      });
    }
    read();
  });
}

function handleEvent(data, planList, execLog) {
  switch (data.type) {
    case 'plan':
      planList.empty();
      data.steps.forEach(function (s, i) {
        planList.append('<div class="plan-step"><span class="step-num">' + (i + 1) + '</span>' + escapeHtml(s) + '</div>');
      });
      break;

    case 'step-start':
      execLog.append('<div class="exec-step active" data-idx="' + data.index + '">' +
        '<span class="exec-status running">\u25CF</span> Step ' + (data.index + 1) + '...</div>');
      break;

    case 'step-end':
      const el = execLog.find('.exec-step[data-idx="' + data.index + '"]');
      if (el.length) {
        el.removeClass('active');
        el.find('.exec-status')
          .removeClass('running')
          .addClass(data.ok ? 'ok' : 'fail')
          .text(data.ok ? '\u2713' : '\u2717');
      }
      break;

    case 'line':
    case 'write':
      const lastStep = execLog.find('.exec-step.active').last();
      if (lastStep.length) {
        let pre = lastStep.find('.exec-output');
        if (!pre.length) {
          pre = $('<pre>').addClass('exec-output').appendTo(lastStep);
        }
        pre.append(document.createTextNode(data.text));
        execLog.scrollTop(execLog[0].scrollHeight);
      }
      break;

    case 'done':
      execLog.append('<div class="exec-step done-final"><span class="exec-status ok">\u2713</span> All steps completed</div>');
      isRunning = false;
      $('#runBtn').prop('disabled', false).text('\u25B6 Run');
      break;

    case 'error':
      execLog.append('<div class="exec-step error-final"><span class="exec-status fail">\u2717</span> ' + escapeHtml(data.text) + '</div>');
      isRunning = false;
      $('#runBtn').prop('disabled', false).text('\u25B6 Run');
      break;
  }
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

// ====== Terminal ======
function toggleTerminal() {
  const panel = $('#terminalPanel');
  panel.toggleClass('collapsed');
  const out = panel.find('.terminal-output, .terminal-input-line');
  out.toggle();
  $('#termToggle').html(panel.hasClass('collapsed') ? '&#x25B2;' : '&#x25BC;');
}

function terminalKeydown(e) {
  if (e.key === 'Enter') {
    const input = $('#terminalInput');
    const cmd = input.val().trim();
    if (!cmd) return;
    input.val('');
    termOutput.append('<div><span style="color:#569cd6">$</span> ' + escapeHtml(cmd) + '</div>');
    termOutput.scrollTop(termOutput[0].scrollHeight);
    $.ajax({
      url: '/api/terminal/run',
      method: 'POST',
      contentType: 'application/json',
      data: JSON.stringify({ command: cmd }),
      success: function (data) {
        termOutput.append('<div>' + escapeHtml(data.output) + '</div>');
        termOutput.scrollTop(termOutput[0].scrollHeight);
      },
      error: function () {
        termOutput.append('<div style="color:#f44747">Error executing command</div>');
      }
    });
  }
}

// ====== Config ======
function openConfig() {
  $('#configModal').addClass('open');
  $.get('/api/config', function (data) {
    $('#cfgApiKey').val(data.apiKey);
    $('#cfgModel').val(data.model);
    $('#cfgEndpoint').val(data.endpoint);
  });
}

function closeConfig() {
  $('#configModal').removeClass('open');
}

function saveConfig() {
  const data = {
    apiKey: $('#cfgApiKey').val().trim(),
    model: $('#cfgModel').val().trim(),
    endpoint: $('#cfgEndpoint').val().trim()
  };
  $.ajax({
    url: '/api/config',
    method: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(data),
    success: function () { closeConfig(); },
    error: function () { alert('Failed to save config'); }
  });
}

// ====== Init ======
$(function () {
  ensureAITab();
  setupAITab();
});
