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
}

export interface PhotoQuery {
  folder?: string;
  type?: string;
  deduplicated?: boolean;
  page?: number;
  pageSize?: number;
}

export interface SlideshowConfigDto {
  intervalSeconds: number;
  transitionMs: number;
}

export interface ConfigDto {
  scanRoots: string[];
  slideshow: SlideshowConfigDto;
}
