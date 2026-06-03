export type FolderType = 'Originals' | 'Edits' | 'Mixed';

export interface PhotoVersionDto {
  id: string;
  fileName: string;
  folderType: FolderType;
  filePath: string;
  isPreferred: boolean;
}

export interface PhotoDto {
  id: string;
  filePath: string;
  fileName: string;
  capturedAt: string | null;
  /** Sort key: capturedAt if present, otherwise fileModifiedAt. Matches cursor/ordering sort. */
  effectiveDate: string | null;
  folderType: FolderType;
  duplicateGroupId: string | null;
  isPreferred: boolean;
  tags: string[];
  /** All versions in this duplicate group, ordered preferred first. Populated on single-photo fetches. */
  versions: PhotoVersionDto[];
}

export interface FolderDto {
  path: string;
  label: string;
  type: FolderType;
  enabled: boolean;
}

export interface PhotoPageDto {
  items: PhotoDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  /** Opaque cursor for the next keyset page. Null when the end of the list has been reached. */
  nextCursor: string | null;
}

export interface PhotoQuery {
  folder?: string;
  type?: string;
  deduplicated?: boolean;
  /** Case-insensitive substring match on the photo's filename (with extension). */
  fileName?: string;
  /** Inclusive lower bound on effective date — ISO date string (YYYY-MM-DD). */
  dateFrom?: string;
  /** Inclusive upper bound on effective date (whole day) — ISO date string (YYYY-MM-DD). */
  dateTo?: string;
  page?: number;
  pageSize?: number;
  /** Keyset cursor — when set, limit must also be set. */
  cursor?: string;
  /** Page size for cursor-based pagination. Takes precedence over page/pageSize. */
  limit?: number;
}

export interface IndexStatusDto {
  complete: boolean;
  count: number;
}

export interface SlideshowConfigDto {
  intervalSeconds: number;
  transitionMs: number;
}

export interface ConfigDto {
  scanRoots: string[];
  slideshow: SlideshowConfigDto;
}

export interface FolderStatsDto {
  path: string;
  label: string;
  type: FolderType;
  photoCount: number;
}

export interface IndexStatsDto {
  complete: boolean;
  totalPhotoCount: number;
  sidecarSizeBytes: number;
  folders: FolderStatsDto[];
}
