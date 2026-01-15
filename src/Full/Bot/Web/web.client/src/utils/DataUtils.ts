
export function getRootUrl(url: string): string {
  let parser = new URL(url);
  let protocol = parser.protocol; // e.g. "https:"
  let domainName = parser.hostname; // e.g. "example.com"
  let port = parser.port; // e.g. "8080"
  if (port && port !== "80" && port !== "443") {
    domainName = `${domainName}:${port}`;
  }
  let rootUrl = `${protocol}//${domainName}`;
  return rootUrl;
}
