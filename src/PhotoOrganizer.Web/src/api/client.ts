import type { ConfigDto, FolderDto, IndexStatsDto, PhotoDto, PhotoPageDto, PhotoQuery } from './types';

async function fetchJson<T>(url: string): Promise<T> {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}: ${url}`);
  return res.json() as Promise<T>;
}

export function imageUrl(id: string): string {
  return `/api/photos/${id}/image`;
}

export async function getFolders(): Promise<FolderDto[]> {
  return fetchJson('/api/folders');
}

export async function getPhotos(query: PhotoQuery = {}): Promise<PhotoPageDto> {
  const params = new URLSearchParams();
  if (query.folder) params.set('folder', query.folder);
  if (query.type) params.set('type', query.type);
  if (query.deduplicated !== undefined) params.set('deduplicated', String(query.deduplicated));
  if (query.page !== undefined) params.set('page', String(query.page));
  if (query.pageSize !== undefined) params.set('pageSize', String(query.pageSize));
  const qs = params.toString();
  return fetchJson(`/api/photos${qs ? `?${qs}` : ''}`);
}

export async function getPhoto(id: string): Promise<PhotoDto | null> {
  const res = await fetch(`/api/photos/${id}`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}: /api/photos/${id}`);
  return res.json() as Promise<PhotoDto>;
}

export async function getConfig(): Promise<ConfigDto> {
  return fetchJson('/api/config');
}

export async function getIndexStats(): Promise<IndexStatsDto> {
  return fetchJson('/api/index/stats');
}

export async function getSlideshowNext(): Promise<PhotoDto | null> {
  const res = await fetch('/api/slideshow/next');
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}: /api/slideshow/next`);
  return res.json() as Promise<PhotoDto>;
}
