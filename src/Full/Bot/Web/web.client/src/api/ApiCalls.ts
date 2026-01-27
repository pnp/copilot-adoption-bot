import { MessageTemplateDto, MessageLogDto, CreateTemplateRequest, UpdateTemplateRequest, MessageBatchDto, CreateBatchAndSendRequest, UpdateLogStatusRequest, ParseFileResponse, MessageStatusStatsDto, UserCoverageStatsDto, QueueStatusDto, CopilotConnectedStatusDto, SmartGroupDto, SmartGroupResolutionResult, CreateSmartGroupRequest, UpdateSmartGroupRequest, PreviewSmartGroupRequest, PreviewSmartGroupResponse, SmartGroupUpnsResponse, AppSettingsDto, UpdateSettingsRequest, CopilotStatsUpdateResponse, CacheOperationResponse, StorageConfigDto } from "../apimodels/Models";
import { BaseAxiosApiLoader } from "./AxiosApiLoader";


// Message Template API calls
export const getAllTemplates = async (loader: BaseAxiosApiLoader): Promise<MessageTemplateDto[]> => {
  return loader.loadFromApi('api/MessageTemplate/GetAll', 'GET');
}

export const getTemplate = async (loader: BaseAxiosApiLoader, id: string): Promise<MessageTemplateDto> => {
  return loader.loadFromApi(`api/MessageTemplate/Get/${id}`, 'GET');
}

export const getTemplateJson = async (loader: BaseAxiosApiLoader, id: string): Promise<{ json: string }> => {
  return loader.loadFromApi(`api/MessageTemplate/GetJson/${id}`, 'GET');
}

export const createTemplate = async (loader: BaseAxiosApiLoader, request: CreateTemplateRequest): Promise<MessageTemplateDto> => {
  return loader.loadFromApi('api/MessageTemplate/Create', 'POST', request);
}

export const updateTemplate = async (loader: BaseAxiosApiLoader, id: string, request: UpdateTemplateRequest): Promise<MessageTemplateDto> => {
  return loader.loadFromApi(`api/MessageTemplate/Update/${id}`, 'PUT', request);
}

export const deleteTemplate = async (loader: BaseAxiosApiLoader, id: string): Promise<void> => {
  return loader.loadFromApi(`api/MessageTemplate/Delete/${id}`, 'DELETE');
}

export const getAllMessageLogs = async (loader: BaseAxiosApiLoader): Promise<MessageLogDto[]> => {
  return loader.loadFromApi('api/MessageTemplate/GetLogs', 'GET');
}

export const getMessageLogsByTemplate = async (loader: BaseAxiosApiLoader, templateId: string): Promise<MessageLogDto[]> => {
  return loader.loadFromApi(`api/MessageTemplate/GetLogsByTemplate/${templateId}`, 'GET');
}

export const getMessageLogsByBatch = async (loader: BaseAxiosApiLoader, batchId: string): Promise<MessageLogDto[]> => {
  return loader.loadFromApi(`api/MessageTemplate/GetLogsByBatch/${batchId}`, 'GET');
}

export const getAllBatches = async (loader: BaseAxiosApiLoader): Promise<MessageBatchDto[]> => {
  return loader.loadFromApi('api/MessageTemplate/GetBatches', 'GET');
}

export const getBatch = async (loader: BaseAxiosApiLoader, id: string): Promise<MessageBatchDto> => {
  return loader.loadFromApi(`api/MessageTemplate/GetBatch/${id}`, 'GET');
}

export const deleteBatch = async (loader: BaseAxiosApiLoader, id: string): Promise<void> => {
  return loader.loadFromApi(`api/MessageTemplate/DeleteBatch/${id}`, 'DELETE');
}

// Send Nudge API calls
export const parseFile = async (loader: BaseAxiosApiLoader, file: File): Promise<ParseFileResponse> => {
  const formData = new FormData();
  formData.append('file', file);
  
  return loader.loadFromApiWithFormData('api/SendNudge/ParseFile', 'POST', formData);
}

export const createBatchAndSend = async (loader: BaseAxiosApiLoader, request: CreateBatchAndSendRequest): Promise<any> => {
  return loader.loadFromApi('api/SendNudge/CreateBatchAndSend', 'POST', request);
}

export const updateLogStatus = async (loader: BaseAxiosApiLoader, logId: string, request: UpdateLogStatusRequest): Promise<void> => {
  return loader.loadFromApi(`api/SendNudge/UpdateLogStatus/${logId}`, 'PUT', request);
}

// Statistics API calls
export const getMessageStatusStats = async (loader: BaseAxiosApiLoader): Promise<MessageStatusStatsDto> => {
  return loader.loadFromApi('api/Statistics/GetMessageStatusStats', 'GET');
}

export const getUserCoverageStats = async (loader: BaseAxiosApiLoader): Promise<UserCoverageStatsDto> => {
  return loader.loadFromApi('api/Statistics/GetUserCoverageStats', 'GET');
}

// Diagnostics API calls
export const getQueueStatus = async (loader: BaseAxiosApiLoader): Promise<QueueStatusDto> => {
  return loader.loadFromApi('api/Diagnostics/QueueStatus', 'GET');
}

export const getStorageConfig = async (loader: BaseAxiosApiLoader): Promise<StorageConfigDto> => {
  return loader.loadFromApi('api/Diagnostics/StorageConfig', 'GET');
}

// Smart Group API calls (Copilot Connected Mode)
export const getCopilotConnectedStatus = async (loader: BaseAxiosApiLoader): Promise<CopilotConnectedStatusDto> => {
  return loader.loadFromApi('api/SmartGroup/CopilotConnectedStatus', 'GET');
}

export const getAllSmartGroups = async (loader: BaseAxiosApiLoader): Promise<SmartGroupDto[]> => {
  return loader.loadFromApi('api/SmartGroup/GetAll', 'GET');
}

export const getSmartGroup = async (loader: BaseAxiosApiLoader, id: string): Promise<SmartGroupDto> => {
  return loader.loadFromApi(`api/SmartGroup/Get/${id}`, 'GET');
}

export const createSmartGroup = async (loader: BaseAxiosApiLoader, request: CreateSmartGroupRequest): Promise<SmartGroupDto> => {
  return loader.loadFromApi('api/SmartGroup/Create', 'POST', request);
}

export const updateSmartGroup = async (loader: BaseAxiosApiLoader, id: string, request: UpdateSmartGroupRequest): Promise<SmartGroupDto> => {
  return loader.loadFromApi(`api/SmartGroup/Update/${id}`, 'PUT', request);
}

export const deleteSmartGroup = async (loader: BaseAxiosApiLoader, id: string): Promise<void> => {
  return loader.loadFromApi(`api/SmartGroup/Delete/${id}`, 'DELETE');
}

export const resolveSmartGroupMembers = async (loader: BaseAxiosApiLoader, id: string, forceRefresh: boolean = false): Promise<SmartGroupResolutionResult> => {
  return loader.loadFromApi(`api/SmartGroup/ResolveMembers/${id}?forceRefresh=${forceRefresh}`, 'POST');
}

export const previewSmartGroup = async (loader: BaseAxiosApiLoader, request: PreviewSmartGroupRequest): Promise<PreviewSmartGroupResponse> => {
  return loader.loadFromApi('api/SmartGroup/Preview', 'POST', request);
}

export const getSmartGroupUpns = async (loader: BaseAxiosApiLoader, id: string): Promise<SmartGroupUpnsResponse> => {
  return loader.loadFromApi(`api/SmartGroup/GetUpns/${id}`, 'GET');
}

// Settings API calls
export const getSettings = async (loader: BaseAxiosApiLoader): Promise<AppSettingsDto> => {
  return loader.loadFromApi('api/Settings/Get', 'GET');
}

export const updateSettings = async (loader: BaseAxiosApiLoader, request: UpdateSettingsRequest): Promise<AppSettingsDto> => {
  return loader.loadFromApi('api/Settings/Update', 'PUT', request);
}

export const resetSettingsToDefaults = async (loader: BaseAxiosApiLoader): Promise<AppSettingsDto> => {
  return loader.loadFromApi('api/Settings/ResetToDefaults', 'POST');
}

// User Cache API calls
export const getCachedUsers = async (loader: BaseAxiosApiLoader): Promise<any[]> => {
  return loader.loadFromApi('api/UserCache/GetCachedUsers', 'GET');
}

export const clearUserCache = async (loader: BaseAxiosApiLoader): Promise<CacheOperationResponse> => {
  return loader.loadFromApi('api/UserCache/Clear', 'POST');
}

export const syncUserCache = async (loader: BaseAxiosApiLoader): Promise<CacheOperationResponse> => {
  return loader.loadFromApi('api/UserCache/Sync', 'POST');
}

export const updateCopilotStats = async (loader: BaseAxiosApiLoader): Promise<CopilotStatsUpdateResponse> => {
  return loader.loadFromApi('api/UserCache/UpdateCopilotStats', 'POST');
}

export const clearCopilotStats = async (loader: BaseAxiosApiLoader): Promise<CacheOperationResponse> => {
  return loader.loadFromApi('api/UserCache/ClearCopilotStats', 'POST');
}

