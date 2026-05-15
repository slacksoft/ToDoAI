let tabs = [];
let activeTabId = null;
let expandedDirs = {};
let rootDir = '';
let isRunning = false;

const fileTree = $('#fileTree');
const tabBar = $('#tabBar');
const tabContent = $('#tabContent');
const termOutput = $('#terminalOutput');

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
  $('#sidebar').toggleClass('open');
  const btn = $('#sidebar').find('.toggle-btn').first();
  if ($('#sidebar').hasClass('open') && !fileTree.children().length) loadFileTree();
}

// ====== Right AI Panel ======
function toggleAIPanel() {
  $('#aiPanel').toggleClass('open');
}

// ====== Theme ======
function toggleTheme() {
  const body = $('body');
  const isDark = body.attr('data-theme') === 'dark';
  body.attr('data-theme', isDark ? 'light' : 'dark');
  $('#themeBtn').html(isDark ? '&#x2600;' : '&#x25D0;');
  const href = isDark
    ? 'https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.8.0/styles/vs.min.css'
    : 'https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.8.0/styles/vs2015.min.css';
  $('#hljsTheme').attr('href', href);
  $('.editor-textarea').each(function () {
    const ta = $(this);
    const pane = ta.closest('.tab-pane');
    const code = pane.find('code')[0];
    const lang = code.className;
    delete code.dataset.highlighted;
    hljs.highlightElement(code);
  });
}

// ====== Tabs ======
function addTab(id, title, isPinned) {
  const existing = tabs.find(function (t) { return t.id === id; });
  if (existing) {
    setActiveTab(id);
    return existing;
  }
  const tab = { id: id, title: title, isPinned: isPinned };
  tabs.push(tab);
  setActiveTab(id);
  if (!isPinned) loadFileContent(id);
  return tab;
}

function closeTab(id) {
  const tab = tabs.find(function (t) { return t.id === id; });
  if (!tab || tab.isPinned) return;
  tabs = tabs.filter(function (t) { return t.id !== id; });
  tabContent.find('#' + escapeId(id)).remove();
  if (activeTabId === id) {
    setActiveTab(tabs.length > 0 ? tabs[tabs.length - 1].id : null);
  } else {
    renderTabs();
    showSaveButton(activeTabId && tabs.some(function (t) { return t.id === activeTabId && !t.isPinned; }));
  }
}

function setActiveTab(id) {
  activeTabId = id;
  renderTabs();
  $('.tab-pane').removeClass('active');
  if (id) {
    const pane = $('#' + escapeId(id));
    if (pane.length) pane.addClass('active');
  }
  showSaveButton(id && !tabs.find(function (t) { return t.id === id; })?.isPinned);
}

function showSaveButton(show) {
  let actions = $('#tabActions');
  if (show && activeTabId) {
    if (!actions.length) {
      actions = $('<div>').attr('id', 'tabActions').addClass('tab-actions');
      const saveBtn = $('<button>').addClass('save-btn').html('&#x1F4BE; Save');
      const delBtn = $('<button>').addClass('delete-btn').html('&#x1F5D1; Delete');
      saveBtn.on('click', function () { saveFile(activeTabId); });
      delBtn.on('click', function () { deleteFile(activeTabId); });
      actions.append(delBtn).append(saveBtn);
      $('.tab-bar').append(actions);
    }
    actions.show();
  } else if (actions.length) {
    actions.hide();
  }
}

function deleteFile(path) {
  if (!confirm('Delete "' + path + '"? This cannot be undone.')) return;
  $.ajax({
    url: '/api/file/delete',
    method: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({ path: path }),
    success: function () {
      closeTab(path);
      refreshFileTree();
    },
    error: function (xhr) {
      alert('Delete failed: ' + xhr.responseText);
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
  if (id === activeTabId) { pane.addClass('active'); }
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

function saveFile(path) {
  const textarea = $('#' + escapeId(path)).find('.editor-textarea');
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

// ====== AI ToDoList ======
function runToDoList() {
  const desc = $('#reqInput').val().trim();
  if (!desc || isRunning) return;
  isRunning = true;
  $('#runBtn').prop('disabled', true).text('\u23F3 Running...');
  $('#aiPanel').addClass('open');

  const out = $('#todolistOutput');
  out.empty();

  const planSection = $('<div>').addClass('todolist-section').appendTo(out);
  $('<div>').addClass('section-title').text('Plan').appendTo(planSection);
  const planList = $('<div>').addClass('plan-list').appendTo(planSection);
  planList.append('<div style="color:var(--text-dim);font-size:11px">Generating plan...</div>');

  const execSection = $('<div>').addClass('todolist-section').appendTo(out);
  $('<div>').addClass('section-title').text('Execution').appendTo(execSection);
  const execLog = $('<div>').addClass('exec-log').appendTo(execSection);

  fetch('/api/todolist/run', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ description: desc })
  }).then(function (response) {
    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    function read() {
      reader.read().then(function (result) {
        if (result.done) { isRunning = false; $('#runBtn').prop('disabled', false).text('\u25B6 Run'); return; }
        const text = decoder.decode(result.value, { stream: true });
        text.split('\n').forEach(function (line) {
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
        '<span class="exec-status running">&#x25CF;</span> Step ' + (data.index + 1) + '...</div>');
      break;
    case 'step-end':
      const el = execLog.find('.exec-step[data-idx="' + data.index + '"]');
      if (el.length) {
        el.removeClass('active');
        el.find('.exec-status').removeClass('running').addClass(data.ok ? 'ok' : 'fail').text(data.ok ? '\u2713' : '\u2717');
      }
      break;
    case 'line':
    case 'write':
      const lastStep = execLog.find('.exec-step.active').last();
      if (lastStep.length) {
        let pre = lastStep.find('.exec-output');
        if (!pre.length) { pre = $('<pre>').addClass('exec-output').appendTo(lastStep); }
        pre.append(document.createTextNode(data.text));
        execLog.scrollTop(execLog[0].scrollHeight);
      }
      break;
    case 'done':
      execLog.append('<div class="exec-step done-final"><span class="exec-status ok">&#x2713;</span> All steps completed</div>');
      isRunning = false;
      $('#runBtn').prop('disabled', false).text('\u25B6 Run');
      break;
    case 'error':
      execLog.append('<div class="exec-step error-final"><span class="exec-status fail">&#x2717;</span> ' + escapeHtml(data.text) + '</div>');
      isRunning = false;
      $('#runBtn').prop('disabled', false).text('\u25B6 Run');
      break;
  }
}

function escapeHtml(text) {
  return $('<span>').text(text).html();
}

// ====== Terminal ======
function toggleTerminal() {
  const panel = $('#terminalPanel');
  panel.toggleClass('collapsed');
  panel.find('.terminal-output, .terminal-input-line').toggle();
  $('#termToggle').html(panel.hasClass('collapsed') ? '&#x25B2;' : '&#x25BC;');
}

function terminalKeydown(e) {
  if (e.key === 'Enter') {
    const input = $('#terminalInput');
    const cmd = input.val().trim();
    if (!cmd) return;
    input.val('');
    termOutput.append('<div><span style="color:var(--accent)">$</span> ' + escapeHtml(cmd) + '</div>');
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
        termOutput.append('<div style="color:var(--danger)">Error executing command</div>');
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
function closeConfig() { $('#configModal').removeClass('open'); }
function saveConfig() {
  const data = {
    apiKey: $('#cfgApiKey').val().trim(),
    model: $('#cfgModel').val().trim(),
    endpoint: $('#cfgEndpoint').val().trim()
  };
  $.ajax({
    url: '/api/config', method: 'POST', contentType: 'application/json',
    data: JSON.stringify(data),
    success: function () { closeConfig(); },
    error: function () { alert('Failed to save config'); }
  });
}

// ====== Init ======
$(function () {});
