import type { ReactNode } from 'react';

/** `**bold**` only. An unmatched `**` stays as typed. */
function inline(text: string): ReactNode[] {
  const parts = text.split('**');
  if (parts.length % 2 === 0) parts.splice(-2, 2, `${parts.at(-2)}**${parts.at(-1)}`);
  return parts.map((part, i) => (i % 2 ? <strong key={i}>{part}</strong> : part));
}

/** The legal-page Markdown subset: `# ` / `## ` headings, blank-line paragraphs, `- ` bullets, `**bold**`.
 *  Builds React elements, so any HTML in the source shows as text. */
export function Markdown({ source }: { source: string }) {
  const blocks: ReactNode[] = [];
  let para: string[] = [];
  let list: string[] = [];
  const flush = () => {
    if (para.length) blocks.push(<p key={blocks.length}>{inline(para.join(' '))}</p>);
    if (list.length)
      blocks.push(
        <ul key={blocks.length}>
          {list.map((item, i) => (
            <li key={i}>{inline(item)}</li>
          ))}
        </ul>,
      );
    para = [];
    list = [];
  };

  for (const raw of source.split(/\r?\n/)) {
    const line = raw.trim();
    if (line.startsWith('# ') || line.startsWith('## ')) {
      flush();
      const Heading = line.startsWith('# ') ? 'h2' : 'h3';
      blocks.push(<Heading key={blocks.length}>{inline(line.replace(/^#+ /, ''))}</Heading>);
    } else if (line.startsWith('- ')) {
      if (para.length) flush();
      list.push(line.slice(2));
    } else if (line) {
      if (list.length) flush();
      para.push(line);
    } else {
      flush();
    }
  }
  flush();
  return <>{blocks}</>;
}
