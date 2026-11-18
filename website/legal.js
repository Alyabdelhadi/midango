// Legal pages: GETs `${data-api}/legal/${data-kind}` and renders the body in the page's language.
// Body format is a small Markdown subset: "# " / "## " headings, blank-line paragraphs, "- " bullets, **bold**.
// Every piece of text goes in through textContent, so HTML in the body shows as text.
const article = document.querySelector('article[data-kind]');
if (article) {
  const ar = document.documentElement.lang === 'ar';
  const loading = article.querySelector('[data-loading]');
  const error = article.querySelector('[data-error]');
  const body = article.querySelector('[data-body]');
  const updatedLabel = article.dataset.updated; // e.g. "Last updated {date}"
  const dateFormat = new Intl.DateTimeFormat(ar ? 'ar-SA-u-ca-gregory-nu-latn' : 'en-GB', { dateStyle: 'long' });

  // Appends text to `parent`, turning **x** into <strong>x</strong>.
  const inline = (parent, text) => {
    text.split('**').forEach((part, i) => {
      if (!part) return;
      if (i % 2) {
        const strong = document.createElement('strong');
        strong.textContent = part;
        parent.append(strong);
      } else {
        parent.append(part);
      }
    });
  };

  const render = (source) => {
    const out = document.createDocumentFragment();
    let paragraph = null;
    let list = null;
    for (const raw of source.replace(/\r\n?/g, '\n').split('\n')) {
      const line = raw.trim();
      const heading = /^(#{1,2}) (.*)$/.exec(line);
      if (!line || heading) { paragraph = null; list = null; }
      if (!line) continue;
      if (heading) {
        const h = document.createElement(heading[1].length === 1 ? 'h1' : 'h2');
        inline(h, heading[2]);
        out.append(h);
      } else if (line.startsWith('- ')) {
        paragraph = null;
        if (!list) { list = document.createElement('ul'); out.append(list); }
        const li = document.createElement('li');
        inline(li, line.slice(2));
        list.append(li);
      } else if (paragraph) {
        inline(paragraph, ' ' + line);
      } else {
        list = null;
        paragraph = document.createElement('p');
        inline(paragraph, line);
        out.append(paragraph);
      }
    }
    return out;
  };

  const load = async () => {
    loading.hidden = false;
    error.hidden = true;
    try {
      const res = await fetch(`${article.dataset.api}/legal/${article.dataset.kind}`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const doc = await res.json();
      const fragment = render((ar ? doc.bodyAr : doc.bodyEn) || '');
      const title = fragment.querySelector('h1');
      if (title) document.title = `${title.textContent} · MIDANGO`;
      if (doc.updatedAt) {
        const updated = document.createElement('p');
        updated.className = 'legal-updated';
        updated.textContent = updatedLabel.replace('{date}', dateFormat.format(new Date(doc.updatedAt)));
        if (title) title.after(updated); else fragment.prepend(updated);
      }
      body.replaceChildren(fragment);
      loading.hidden = true;
    } catch {
      loading.hidden = true;
      error.hidden = false;
    }
  };

  error.querySelector('button').addEventListener('click', load);
  load();
}
