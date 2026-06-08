<script>
  import { onMount } from 'svelte';
  
  let activeSection = 'intro';
  
  const sections = [
    { id: 'intro', title: 'Введение' },
    { id: 'template-vars', title: 'Переменные трека' },
    { id: 'css-vars', title: 'CSS цвета' },
    { id: 'data-bind', title: 'Динамика (data-bind)' },
    { id: 'structure', title: 'Структура HTML' },
    { id: 'examples', title: 'Примеры' },
    { id: 'tips', title: 'Советы' }
  ];
  
  function scrollTo(id) {
    activeSection = id;
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
</script>

<div class="guide-container">
  <!-- Sidebar navigation -->
  <nav class="sidebar">
    <h2 class="sidebar-title">📖 Гайд</h2>
    <ul>
      {#each sections as section}
        <li>
          <button 
            class="nav-item" 
            class:active={activeSection === section.id}
            on:click={() => scrollTo(section.id)}
          >
            {section.title}
          </button>
        </li>
      {/each}
    </ul>
  </nav>

  <!-- Main content -->
  <main class="content">
    
    <!-- INTRO -->
    <section id="intro" class="section">
      <h1>🎨 Как создать кастомный плеер</h1>
      <p class="lead">
        UnikPlayer позволяет создавать свои HTML-виджеты для отображения текущего трека. 
        Это обычный HTML + CSS файл с специальными переменными, который рендерится в OBS.
      </p>
      
      <div class="info-box">
        <h3>📁 Где хранятся плееры?</h3>
        <p>Кастомные плееры находятся в папке:</p>
        <pre><code>%LOCALAPPDATA%\UnikPlayer\custom\    (Windows)
dev-data/custom/                      (Dev mode)</code></pre>
        <p>Файлы должны иметь расширение <code>.html</code></p>
      </div>

      <div class="info-box success">
        <h3>✅ Что можно делать</h3>
        <ul>
          <li>Использовать любой HTML и CSS</li>
          <li>Подключать внешние шрифты (Google Fonts и др.)</li>
          <li>Использовать CSS переменные для цветов из обложки</li>
          <li>Добавлять анимации и переходы</li>
          <li>Использовать <code>data-bind</code> для динамического обновления (время, прогресс)</li>
          <li>Делать прозрачный фон для OBS (используй <code>background: transparent</code>)</li>
        </ul>
      </div>

      <div class="info-box error">
        <h3>❌ Что НЕЛЬЗЯ делать</h3>
        <ul>
          <li>Использовать JavaScript для логики (JS не выполняется, только для динамики через data-bind)</li>
          <li>Загружать внешние изображения (только через переменные обложки)</li>
          <li>Использовать сложные фреймворки (React, Vue и т.д.)</li>
        </ul>
      </div>
    </section>

    <!-- TEMPLATE VARIABLES -->
    <section id="template-vars" class="section">
      <h2>🏷️ Переменные трека (Template Variables)</h2>
      <p>Эти переменные заменяются на реальные данные трека. Просто вставь их в HTML.</p>

      <table class="vars-table">
        <thead>
          <tr>
            <th>Переменная</th>
            <th>Описание</th>
            <th>Пример значения</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td><code>{{title}}</code></td>
            <td>Название трека</td>
            <td>Blinding Lights</td>
          </tr>
          <tr>
            <td><code>{{artist}}</code></td>
            <td>Исполнитель</td>
            <td>The Weeknd</td>
          </tr>
          <tr>
            <td><code>{{thumbnail}}</code></td>
            <td>URL обложки альбома (для тега <code><<co></code>de><img></code>)</td>
            <td><code>data:image/jpeg;base64,...</code></td>
          </tr>
        </tbody>
      </table>

      <h3>Пример использования:</h3>
      <h3>Пример использования:</h3></p>
     div lass="c-example"
        <pre><code><div class="track">
  <img src="humbna" alt="vr"
 div lass="inf">
    <iv class="titl"leiv
   iv class="artist"rtstdiv>
  </div>
</div></re
        div>
    </<p>В HTML файле плеера используй переменные так:</p>
      <div class="code-example">
        <pre><code><div class="track">
  <img src="{{thumbnail}}" alt="cover">
  <div class="info">
    <div class="title">{{title}}</div>
    <div class="artist">{{artist}}</div>
  </div>
</div></code></pre>
      </div>
    </section>

    <!-- CSS VARIABLES -->
    <section id="css-vars" class="section">
      <h2>🎨 CSS переменные цветов (из обложки)</h2>
      <p>
        UnikPlayer автоматически извлекает цвета из обложки альбома и делает их доступными 
        как CSS переменные. Это позволяет плееру подстраиваться под цвета трека!
      </p>

      <table class="vars-table">
        <thead>
          <tr>
            <th>CSS переменная</th>
            <th>Описание</th>
            <th>Пример</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td><code>var(--vibrant)</code></td>
            <td>Основной яркий цвет</td>
            <td style="background: #ff6b6b; color: white;">#ff6b6b</td>
          </tr>
          <tr>
            <td><code>var(--lightVibrant)</code></td>
            <td>Светлый яркий</td>
            <td style="background: #ffa8a8; color: black;">#ffa8a8</td>
          </tr>
          <tr>
            <td><code>var(--darkVibrant)</code></td>
            <td>Тёмный яркий</td>
            <td style="background: #c92a2a; color: white;">#c92a2a</td>
          </tr>
          <tr>
            <td><code>var(--muted)</code></td>
            <td>Приглушённый</td>
            <td style="background: #868e96; color: white;">#868e96</td>
          </tr>
          <tr>
            <td><code>var(--lightMuted)</code></td>
            <td>Светлый приглушённый</td>
            <td style="background: #ced4da; color: black;">#ced4da</td>
          </tr>
          <tr>
            <td><code>var(--darkMuted)</code></td>
            <td>Тёмный приглушённый (отлично для фона)</td>
            <td style="background: #343a40; color: white;">#343a40</td>
          </tr>
        </tbody>
      </table>

      <div class="color-preview">
        <h3>Пример палитры (на основе обложки):</h3>
       h3<Пример иiv claования=в "pa:</h3>
l     <div class="code-example">
        <pre><code>.track-title &#123;
  color: var(--lightVibrant);  /* Цвtт из обложки */
  font-size: 1.2rem;
&#125;

.player-card &#123;
  background: var(--darkMuted);  /* Тё"йфон под обложу*/
  brr: 2px solid ;
&#125;</pre>
   div
          <div class="color-swatch" style="background: var(--vibrant);">vibrant</div>
          <div class="color-swatch" style="background: var(--lightVibrant); color: #000;">lightVibrant</div>
          <div class="color-swatch" style="background: var(--darkVibrant);">darkVibrant</div>
          <div class="color-swatch" style="background: var(--muted);">muted</div>
          <div class="color-swatch" style="background: var(--lightMuted); color: #000;">lightMuted</div>:
          <div class="color-swatch" style="background: var(--darkMuted);">darkMuted</div>
         div class="code-example">
          <pre><code> * Неправильно */
.title &#123; color: var(--lightVibrant); &#125;

/* Правильно */
.title * &#123; color: var(--lightVibrant); &#125;</co<e></pre>
        </d/div>
        div>
    </</div>

      <h3>Пример использования в CSS:</h3>
      <div class="code-example">
        <pre><code>.track-title &#123;
  color: var(--lightVibrant);  /* Цвет из обложки */
  font-size: 1.2rem;
&#125;

.player-card &#123;
  background: var(--darkMuted);  /* Тёмный фон под обложку */
  border: 2px solid var(--vibrant);
&#125;</code></pre>
      </div>

      <div class="info-box">
        <h3>💡 Важно про текст</h3>
        <p>
          Если используешь библиотеку для бегущей строки (marquee), стилизуй через <code>.class *</code>:
        </p>
        <div class="code-example">
          <pre><code>/* Неправильно */
.title &#123; color: var(--lightVibrant); &#125;

/* Правильно */
.title * &#123; color: var(--lightVibrant); &#125;</code></pre>
        </div>
      </div>
    </section>

    <!-- DATA-BIND -->
    <section id="data-bind" class="section">
      <h2>⚡ Динамические данные (data-bind)</h2><coe><d></code>
      <p>
        Для обновления элементов в реальном времени (без перезагрузки) используй атрибут <code>data-bind</code>.
        Бэкенд сам обновляет эти элементы через WebSocket.
      </p><coe>dcode></
<code></code>
      <table class="vars-table">
        <thead>
          <tr>
            <th>data-bind значение</th>
       h3    <thЧ рдгресс-б<ра:</h3>
>     <iv clss="code-example">
        <pre><code><div class="progress-coniner">
  <span data="">0:00</span>
 <div class="progress-bar">
    <div class="progress-fill" daa-bind="prgress-widh"></div>
  </div>
  <spn data-bind="tota">0:00</span>
</div></code></pre>
     </div>

      <h3>Пример индикатора Playing/Paused:</h3>
      <div class="code-examle">
        <pe><cde><div class="status" data-bind="playing" data-playin="false"></div>

&lt;styl&gt;
  .tatu[dataplayng="true"]::before &#123; content: 'PLAYING'; &#125;
  .status[aa-ng="false"]::before &#123; contet: 'PAUSED'; &#125;
&lt;/style&t;</code>re>
      </div
            <th>Тип элемента</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td><code>currentTime</code></td>:
            <td>Текущее время трека (формат: "1:23")</td>
            <td>Любой элемент с текстом</td>
          </tr>
          <tr>
            <td><code>totalTime</code></td>
            <td>Общая длительность трека (формат: "3:45")</td><></code>:
        p>
        <div class="e-exampl"
          <pre><code><img src="{{thumbnail}}" alt="Album cover"></code>   re>
        </div <td>Любой элемент с текстом</td>
          </tr>
          <tr>
            <td><code>progress-width</code></td>
            <td>Ширина прогресс-бара (в процентах, например "45%")</td>
            <td>Элемент с шириной (обычно вложенный <code><div></code>)</td>
          </tr>
          <tr>
            <td><code>playing</code></td>
            <td>Статус воспроизведения. Добавляет атрибут <code>data-playing="true/false"</code></td>
            <td>Любой элемент (используй в CSS через <code>[data-playing="true"]</code>)</td>
          </tr>
        </tbody>
      </table>

      <h3>Пример прогресс-бара:</h3>
      <div class="code-example">
        <pre><code><div class="progress-container">
  <span data-bind="currentTime">0:00</span>
  <div class="progress-bar">
    <div class="progress-fill" data-bind="progress-width"></div>
  </div>
  <span data-bind="totalTime">0:00</span>
</div></code></pre>
      </div>

      <h3>Пример индикатора Playing/Paused:</h3>
      <div class="code-example">
        <pre><code><div class="status" data-bind="playing" data-playing="false"></div>

&lt;style&gt;
  .status[data-playing="true"]::before &#123; content: 'PLAYING'; &#125;
  .status[data-playing="false"]::before &#123; content: 'PAUSED'; &#125;
&lt;/style&gt;</code></pre>
      </div>
    </section>

    <!-- STRUCTURE -->
    <section id="structure" class="section">
      <h2>📐 Структура HTML файла</h2>
      <p>Минимальный шаблон для кастомного плеера:</p>

      <div class="info-box">
        <h3>🖼️ Про обложку (thumbnail)</h3><><>
        <p>
          Переменная <code>{{thumbnail}}</code> заменяется на <code>data:image/jpeg;base64,...</code> - 
          это встроенное изображение. Используй её только в атрибуте <code>src</code> тега <code><img></code>:
        </p>
        <div class="code-example">
          <pre><code><img src="{{thumbnail}}" alt="Album cover"></code></pre>
        </div>
        <p>Не пытайся использовать <code>{{thumbnail}}</code> как CSS background-image - это не сработает!</p>
      </div>
    </section>

    <!-- EXAMPLES -->
    <section id="examples" class="section">
      <h2>💡 Готовые примеры</h2>
      <p>Примеры кода для создания кастомных плееров находятся в папке <code>backend-csharp/UnikPlayer/example-players/</code>. Скопируй любой из них в папку <code>dev-data/custom/</code> или <code>%LOCALAPPDATA%\UnikPlayer\custom\</code> чтобы начать.</p>
    </section>

    <!-- TIPS -->
    <section id="tips" class="section">
      <h2>🎯 Советы и лучшие практики</h2>

      <div class="tips-grid">
        <div class="tip-card">
          <h3>📏 Размер</h3>
          <p>Делай плеер фиксированной ширины (например <code>20rem</code> или <code>350px</code>), чтобы он хорошо смотрелся в OBS.</p>
        </div>

        <div class="tip-card">
          <h3>🎨 Цвета</h3>
          <p>Используй <code>var(--darkMuted)</code> для фона и <code>var(--lightVibrant)</code> для текста - это обеспечит читаемость на любой обложке.</p>
        </div>

        <div class="tip-card">
          <h3>🖼️ Прозрачность</h3>
          <p>Для OBS всегда используй <code>background: transparent</code> на <code>body</code>. Иначе будет белый фон.</p>
        </div>

        <div class="tip-card">
          <h3>📱 Overflow</h3>
          <p>Добавляй <code>overflow: hidden; text-overflow: ellipsis; white-space: nowrap;</code> для длинных названий треков.</p>
        </div>

        <div class="tip-card">
          <h3>⏱️ Время</h3>
          <p>Для отображения времени используй <code>data-bind="currentTime"</code> и <code>data-bind="totalTime"</code> - они обновляются каждую секунду.</p>
        </div>

        <div class="tip-card">
          <h3>🔤 Шрифты</h3>
          <p>Можно подключать Google Fonts через <code><link></code> в <code><head></code>. Но помни - чем больше шрифт, тем дольше загрузка.</p>
        </div>
      </div>

      <div class="info-box success">
        <h3>🚀 Быстрый старт</h3>
        <ol>
          <li>Скопируй один из примеров из <code>backend-csharp/UnikPlayer/example-players/</code></li>
          <li>Сохрани как <code>.html</code> файл в папку <code>dev-data/custom/</code> (или <code>%LOCALAPPDATA%\UnikPlayer\custom\</code>)</li>
          <li>Перезапусти UnikPlayer</li>
          <li>Выбери свой плеер в редакторе</li>
          <li>Наслаждайся!</li>
        </ol>
      </div>
    </section>

  </main>
</div>

<style>
  .guide-container {
    display: flex;
    gap: 2rem;
    max-width: 1200px;
    margin: 0 auto;
    padding: 2rem;
  }

  /* Sidebar */
  .sidebar {
    position: sticky;
    top: 80px;
    height: fit-content;
    width: 220px;
    flex-shrink: 0;
    background: color-mix(in srgb, var(--c2) 90%, transparent);
    border-radius: 0.75rem;
    padding: 1.2rem;
    border: 1px solid color-mix(in srgb, var(--c1) 10%, transparent);
  }

  .sidebar-title {
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: var(--c1);
    margin-bottom: 1rem;
    padding-bottom: 0.5rem;
    border-bottom: 2px solid var(--c1);
  }

  .sidebar ul {
    list-style: none;
    padding: 0;
    margin: 0;
  }

  .nav-item {
    display: block;
    width: 100%;
    text-align: left;
    background: none;
    border: none;
    padding: 0.5rem 0.8rem;
    margin-bottom: 0.3rem;
    border-radius: 0.4rem;
    cursor: pointer;
    font-size: 0.85rem;
    color: var(--c-text);
    transition: all 0.2s;
    font-family: inherit;
  }

  .nav-item
  h3 {
    color: #cdd6f4;
    padding: 1rem 1.2rem;
    border-radius: 0.5rem;
    font-family: 'Fira Code', 'Consolas', monospace;
    font-size: 1rem;
    color: var(--c-text);
  }

  /* Info boxes */
  .info-box {
    background: color-mix(in srgb, var(--c2) 95%, transparent);
    border-left: 4px solid var(--c1);
    padding: 1rem 1.2rem;
    border-radius: 0.5rem;
    margin: 1.5rem 0;
  }

  .info-box.success {
    border-left-color: #10b981;
    background: rgba(16, 185, 129, 0.05);
  }

  .info-box.error {
    border-left-color: #ef4444;
    background: rgba(239, 68, 68, 0.05);
  }

  .info-box h3 {
    margin-top: 0;
    margin-bottom: 0.8rem;
  }

  .info-box ul {
    margin: 0;
    padding-left: 1.2rem;
  }

  .info-box li {
    margin-bottom: 0.4rem;
    line-height: 1.5;
  }

  /* Code blocks */
  .code-example {
    margin: 1rem 0;
  }

  pre {
    display: block;
    background: #1e1e2e;
    color: #cdd6f4;
    padding: 1rem 1.2rem;
    border-radius: 0.5rem;
    font-family: 'Fira Code', 'Consolas', monospace;
    font-size: 1rem;
    line-height: 1.5;
    overflow-x: auto;
    white-space: pre;
    margin: 0;
  }

  pre code {
    background: none;
    padding: 0;
    border-radius: 0;
    font-family: inherit;
    font-size: inherit;
    color: inherit;
  }

  code {
    background: color-mix(in srgb, var(--c1) 15%, transparent);
    padding: 0.15rem 0.4rem;
    border-radius: 0.3rem;
    font-family: 'Fira Code', monospace;
    font-size: 0.85em;
    color: var(--c1);
  }

  /* Tables */
  .vars-table {
    width: 100%;
    border-collapse: collapse;
    margin: 1rem 0;
    background: color-mix(in srgb, var(--c2) 95%, transparent);
    border-radius: 0.5rem;
    overflow: hidden;
  }

  .vars-table th {
    background: var(--c1);
    color: white;
    padding: 0.7rem 1rem;
    text-align: left;
    font-size: 0.9rem;
  }

  .vars-table td {
    padding: 0.6rem 1rem;
    border-bottom: 1px solid color-mix(in srgb, var(--c1) 10%, transparent);
    font-size: 0.9rem;
  }

  .vars-table tr:last-child td {
    border-bottom: none;
  }

  .vars-table code {
    background: rgba(0,0,0,0.2);
    color: #f38ba8;
  }

  /* Color preview */
  .color-preview {
    margin: 1.5rem 0;
  }

  .palette {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
    gap: 0.5rem;
    margin-top: 1rem;
  }

  .color-swatch {
    padding: 1rem;
    border-radius: 0.5rem;
    text-align: center;
    font-size: 0.8rem;
    font-weight: 600;
    font-family: monospace;
  }

  /* Tips grid */
  .tips-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
    gap: 1rem;
    margin: 1.5rem 0;
  }

  .tip-card {
    background: color-mix(in srgb, var(--c2) 95%, transparent);
    padding: 1rem 1.2rem;
    border-radius: 0.5rem;
    border: 1px solid color-mix(in srgb, var(--c1) 10%, transparent);
  }

  .tip-card h3 {
    margin-top: 0;
    margin-bottom: 0.5rem;
    font-size: 1rem;
  }

  .tip-card p {
    margin-bottom: 0;
    font-size: 0.9rem;
  }

  ol {
    padding-left: 1.5rem;
    line-height: 1.8;
  }

  ol li {
    margin-bottom: 0.5rem;
  }

  @media (max-width: 768px) {
    .guide-container {
      flex-direction: column;
    }

    .sidebar {
      position: static;
      width: 100%;
    }
  }
</style>