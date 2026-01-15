import { useState, useEffect, useCallback } from 'react';
import { BaseAxiosApiLoader } from '../../../api/AxiosApiLoader';
import {
    getAllTemplates,
    createTemplate,
    updateTemplate,
    deleteTemplate,
    getTemplateJson
} from '../../../api/ApiCalls';
import { MessageTemplateDto, CreateTemplateRequest, UpdateTemplateRequest } from '../../../apimodels/Models';

export interface UseTemplatesResult {
    templates: MessageTemplateDto[];
    loading: boolean;
    error: string | null;
    loadTemplates: () => Promise<void>;
    handleCreate: (templateName: string, jsonPayload: string) => Promise<boolean>;
    handleUpdate: (id: string, templateName: string, jsonPayload: string) => Promise<boolean>;
    handleDelete: (id: string) => Promise<boolean>;
    getJson: (id: string) => Promise<string | null>;
    clearError: () => void;
}

export const useTemplates = (loader?: BaseAxiosApiLoader): UseTemplatesResult => {
    const [templates, setTemplates] = useState<MessageTemplateDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const loadTemplates = useCallback(async () => {
        if (!loader) return;

        try {
            setLoading(true);
            setError(null);
            const data = await getAllTemplates(loader);
            setTemplates(data);
        } catch (err: any) {
            setError(err.message || 'Failed to load templates');
            console.error('Error loading templates:', err);
        } finally {
            setLoading(false);
        }
    }, [loader]);

    useEffect(() => {
        loadTemplates();
    }, [loadTemplates]);

    const handleCreate = useCallback(async (templateName: string, jsonPayload: string): Promise<boolean> => {
        if (!loader) return false;

        try {
            const request: CreateTemplateRequest = {
                templateName,
                jsonPayload
            };
            await createTemplate(loader, request);
            await loadTemplates();
            return true;
        } catch (err: any) {
            setError(err.message || 'Failed to create template');
            console.error('Error creating template:', err);
            return false;
        }
    }, [loader, loadTemplates]);

    const handleUpdate = useCallback(async (id: string, templateName: string, jsonPayload: string): Promise<boolean> => {
        if (!loader) return false;

        try {
            const request: UpdateTemplateRequest = {
                templateName,
                jsonPayload
            };
            await updateTemplate(loader, id, request);
            await loadTemplates();
            return true;
        } catch (err: any) {
            setError(err.message || 'Failed to update template');
            console.error('Error updating template:', err);
            return false;
        }
    }, [loader, loadTemplates]);

    const handleDelete = useCallback(async (id: string): Promise<boolean> => {
        if (!loader) return false;

        try {
            await deleteTemplate(loader, id);
            await loadTemplates();
            return true;
        } catch (err: any) {
            setError(err.message || 'Failed to delete template');
            console.error('Error deleting template:', err);
            return false;
        }
    }, [loader, loadTemplates]);

    const getJson = useCallback(async (id: string): Promise<string | null> => {
        if (!loader) return null;

        try {
            const response = await getTemplateJson(loader, id);
            return response.json;
        } catch (err: any) {
            setError(err.message || 'Failed to load template JSON');
            console.error('Error loading template JSON:', err);
            return null;
        }
    }, [loader]);

    const clearError = useCallback(() => {
        setError(null);
    }, []);

    return {
        templates,
        loading,
        error,
        loadTemplates,
        handleCreate,
        handleUpdate,
        handleDelete,
        getJson,
        clearError
    };
};
