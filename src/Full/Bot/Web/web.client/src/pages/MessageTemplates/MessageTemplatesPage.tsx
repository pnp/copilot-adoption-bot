import React, { useState } from 'react';
import {
    Spinner,
    Text,
    makeStyles,
    tokens
} from '@fluentui/react-components';
import { BaseAxiosApiLoader } from '../../api/AxiosApiLoader';
import { MessageTemplateDto } from '../../apimodels/Models';
import { useTemplates } from './hooks/useTemplates';
import {
    TemplateFormDialog,
    TemplateViewDialog,
    TemplateDeleteDialog,
    TemplateTable
} from './components';

const useStyles = makeStyles({
    container: {
        padding: tokens.spacingVerticalXXL,
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: tokens.spacingVerticalL,
    },
    error: {
        color: tokens.colorPaletteRedForeground1,
        marginTop: tokens.spacingVerticalM,
    }
});

interface MessageTemplatesPageProps {
    loader?: BaseAxiosApiLoader;
}

export const MessageTemplatesPage: React.FC<MessageTemplatesPageProps> = ({ loader }) => {
    const styles = useStyles();
    const {
        templates,
        loading,
        error,
        handleCreate,
        handleUpdate,
        handleDelete,
        getJson
    } = useTemplates(loader);

    // Dialog states
    const [createDialogOpen, setCreateDialogOpen] = useState(false);
    const [editDialogOpen, setEditDialogOpen] = useState(false);
    const [viewDialogOpen, setViewDialogOpen] = useState(false);
    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

    // Form states
    const [templateName, setTemplateName] = useState('');
    const [jsonPayload, setJsonPayload] = useState('');
    const [selectedTemplate, setSelectedTemplate] = useState<MessageTemplateDto | null>(null);
    const [viewJson, setViewJson] = useState('');

    const onCreateSubmit = async () => {
        const success = await handleCreate(templateName, jsonPayload);
        if (success) {
            setCreateDialogOpen(false);
            setTemplateName('');
            setJsonPayload('');
        }
    };

    const onEditSubmit = async () => {
        if (!selectedTemplate) return;
        const success = await handleUpdate(selectedTemplate.id, templateName, jsonPayload);
        if (success) {
            setEditDialogOpen(false);
            setSelectedTemplate(null);
            setTemplateName('');
            setJsonPayload('');
        }
    };

    const onDeleteConfirm = async () => {
        if (!selectedTemplate) return;
        const success = await handleDelete(selectedTemplate.id);
        if (success) {
            setDeleteDialogOpen(false);
            setSelectedTemplate(null);
        }
    };

    const openViewDialog = async (template: MessageTemplateDto) => {
        const json = await getJson(template.id);
        if (json !== null) {
            setViewJson(json);
            setSelectedTemplate(template);
            setViewDialogOpen(true);
        }
    };

    const openEditDialog = async (template: MessageTemplateDto) => {
        const json = await getJson(template.id);
        if (json !== null) {
            setSelectedTemplate(template);
            setTemplateName(template.templateName);
            setJsonPayload(json);
            setEditDialogOpen(true);
        }
    };

    const openDeleteDialog = (template: MessageTemplateDto) => {
        setSelectedTemplate(template);
        setDeleteDialogOpen(true);
    };

    const handleCreateDialogOpenChange = (open: boolean) => {
        if (open) {
            // Reset form when opening create dialog
            setTemplateName('');
            setJsonPayload('');
        }
        setCreateDialogOpen(open);
    };

    const handleEditDialogOpenChange = (open: boolean) => {
        if (!open) {
            // Reset form when closing edit dialog
            setTemplateName('');
            setJsonPayload('');
            setSelectedTemplate(null);
        }
        setEditDialogOpen(open);
    };

    const handleViewDialogOpenChange = (open: boolean) => {
        if (!open) {
            setViewJson('');
            setSelectedTemplate(null);
        }
        setViewDialogOpen(open);
    };

    const handleDeleteDialogOpenChange = (open: boolean) => {
        if (!open) {
            setSelectedTemplate(null);
        }
        setDeleteDialogOpen(open);
    };

    if (loading) {
        return <Spinner label="Loading templates..." />;
    }

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <div>
                    <h1>Message Templates</h1>
                    <Text>Manage Teams adaptive card message templates</Text>
                </div>
                <TemplateFormDialog
                    mode="create"
                    open={createDialogOpen}
                    onOpenChange={handleCreateDialogOpenChange}
                    templateName={templateName}
                    onTemplateNameChange={setTemplateName}
                    jsonPayload={jsonPayload}
                    onJsonPayloadChange={setJsonPayload}
                    onSubmit={onCreateSubmit}
                    triggerButton={true}
                />
            </div>

            {error && <div className={styles.error}>{error}</div>}

            <TemplateTable
                templates={templates}
                onView={openViewDialog}
                onEdit={openEditDialog}
                onDelete={openDeleteDialog}
            />

            <TemplateFormDialog
                mode="edit"
                open={editDialogOpen}
                onOpenChange={handleEditDialogOpenChange}
                templateName={templateName}
                onTemplateNameChange={setTemplateName}
                jsonPayload={jsonPayload}
                onJsonPayloadChange={setJsonPayload}
                onSubmit={onEditSubmit}
            />

            <TemplateViewDialog
                open={viewDialogOpen}
                onOpenChange={handleViewDialogOpenChange}
                templateName={selectedTemplate?.templateName ?? ''}
                json={viewJson}
            />

            <TemplateDeleteDialog
                open={deleteDialogOpen}
                onOpenChange={handleDeleteDialogOpenChange}
                templateName={selectedTemplate?.templateName ?? ''}
                onConfirm={onDeleteConfirm}
            />
        </div>
    );
};
