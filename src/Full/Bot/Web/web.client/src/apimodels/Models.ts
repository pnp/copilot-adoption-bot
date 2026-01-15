export interface BaseDTO { id?: string }

export interface ServiceConfiguration {
  storageInfo: StorageInfo;
}

export interface StorageInfo {
  accountURI: string;
  sharedAccessToken: string;
}

export interface MessageTemplateDto {
  id: string;
  templateName: string;
  blobUrl: string;
  createdByUpn: string;
  createdDate: string;
}

export interface MessageBatchDto {
  id: string;
  batchName: string;
  templateId: string;
  senderUpn: string;
  createdDate: string;
}

export interface MessageLogDto {
  id: string;
  messageBatchId: string;
  sentDate: string;
  recipientUpn?: string;
  status: string;
  lastError?: string;
}

export interface CreateTemplateRequest {
  templateName: string;
  jsonPayload: string;
}

export interface UpdateTemplateRequest {
  templateName: string;
  jsonPayload: string;
}

export interface CreateBatchAndSendRequest {
  batchName: string;
  templateId: string;
  recipientUpns?: string[];
  smartGroupIds?: string[];
}

export interface UpdateLogStatusRequest {
  status: string;
  lastError?: string;
}

export interface ParseFileResponse {
  upns: string[];
}

export interface MessageStatusStatsDto {
  sentCount: number;
  failedCount: number;
  pendingCount: number;
  totalCount: number;
}

export interface UserCoverageStatsDto {
  usersMessaged: number;
  totalUsersInTenant: number;
  usersNotMessaged: number;
  coveragePercentage: number;
}

export interface QueueStatusDto {
  success: boolean;
  queueLength: number;
  timestamp: string;
}

// Smart Group Models (Copilot Connected Mode)

export interface CopilotConnectedStatusDto {
  isEnabled: boolean;
  hasAIFoundryConfig: boolean;
}

export interface SmartGroupDto {
  id: string;
  name: string;
  description: string;
  createdByUpn: string;
  createdDate: string;
  lastResolvedDate?: string;
  lastResolvedMemberCount?: number;
}

export interface SmartGroupMemberDto {
  userPrincipalName: string;
  displayName?: string;
  department?: string;
  jobTitle?: string;
  confidenceScore?: number;
}

export interface SmartGroupResolutionResult {
  smartGroupId: string;
  smartGroupName: string;
  members: SmartGroupMemberDto[];
  resolvedAt: string;
  fromCache: boolean;
}

export interface CreateSmartGroupRequest {
  name: string;
  description: string;
}

export interface UpdateSmartGroupRequest {
  name: string;
  description: string;
}

export interface PreviewSmartGroupRequest {
  description: string;
  maxUsers?: number;
}

export interface PreviewSmartGroupResponse {
  members: SmartGroupMemberDto[];
  count: number;
}

export interface SmartGroupUpnsResponse {
  upns: string[];
  count: number;
}

// Application Settings Models

export interface AppSettingsDto {
  followUpChatSystemPrompt: string | null;
  defaultFollowUpChatSystemPrompt: string;
  lastModifiedDate: string | null;
  lastModifiedByUpn: string | null;
}

export interface UpdateSettingsRequest {
  followUpChatSystemPrompt: string | null;
}

// User Cache Models

export interface CopilotStatsUpdateResponse {
  message: string;
  success: boolean;
  lastUpdate?: string;
  error?: string;
}

export interface CacheOperationResponse {
  message: string;
  success?: boolean;
  error?: string;
}

// ID Token Claims for MSAL authentication
export interface IdTokenClaims {
  given_name?: string;
  family_name?: string;
  name?: string;
  preferred_username?: string;
  oid?: string;
  tid?: string;
  sub?: string;
  email?: string;
}

// Survey Page Models
export interface SurveyPageDTO {
  id: string;
  title: string;
  description?: string;
  cardPayload?: string;
}


