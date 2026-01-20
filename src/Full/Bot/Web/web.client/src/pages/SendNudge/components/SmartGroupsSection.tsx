import React from 'react';
import {
    Button,
    Text,
    Checkbox,
    DialogTrigger,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import {
    Sparkle20Regular,
    PeopleTeam20Regular,
    Edit20Regular,
    Delete20Regular,
    AddCircle20Regular,
    Info20Regular,
} from '@fluentui/react-icons';
import { SmartGroupDto } from '../../../apimodels/Models';

const useStyles = makeStyles({
    smartGroupSection: {
        marginTop: tokens.spacingVerticalM,
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorBrandStroke1}`,
    },
    smartGroupItem: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: tokens.spacingVerticalS,
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusSmall,
        marginBottom: tokens.spacingVerticalXS,
    },
    smartGroupInfo: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
});

interface SmartGroupsSectionProps {
    smartGroups: SmartGroupDto[];
    selectedSmartGroupIds: string[];
    deletingSmartGroupId: string | null;
    onToggle: (groupId: string, checked: boolean) => void;
    onEdit: (group: SmartGroupDto) => void;
    onDelete: (groupId: string) => void;
    onShowHelp: () => void;
    onShowCreateDialog: () => void;
}

export const SmartGroupsSection: React.FC<SmartGroupsSectionProps> = ({
    smartGroups,
    selectedSmartGroupIds,
    deletingSmartGroupId,
    onToggle,
    onEdit,
    onDelete,
    onShowHelp,
    onShowCreateDialog,
}) => {
    const styles = useStyles();

    return (
        <div className={styles.smartGroupSection}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: tokens.spacingVerticalS }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                    <Sparkle20Regular />
                    <Text weight="semibold">Smart Groups</Text>
                    <Button
                        size="small"
                        appearance="subtle"
                        icon={<Info20Regular />}
                        onClick={onShowHelp}
                        title="View example user data for AI prompts"
                    />
                </div>
                <Button size="small" icon={<AddCircle20Regular />} onClick={onShowCreateDialog}>
                    Create Smart Group
                </Button>
            </div>

            {smartGroups.length === 0 ? (
                <Text size={200}>No smart groups created yet. Create one to use AI-powered user targeting.</Text>
            ) : (
                smartGroups.map(group => (
                    <div key={group.id} className={styles.smartGroupItem}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                            <Checkbox
                                checked={selectedSmartGroupIds.includes(group.id)}
                                onChange={(_, data) => onToggle(group.id, data.checked as boolean)}
                            />
                            <div className={styles.smartGroupInfo}>
                                <Text weight="semibold">{group.name}</Text>
                                <Text size={200}>{group.description}</Text>
                            </div>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                            <PeopleTeam20Regular />
                            <Text size={200}>
                                {group.lastResolvedMemberCount != null 
                                    ? `${group.lastResolvedMemberCount} members` 
                                    : 'Not resolved yet'}
                            </Text>
                            <Button
                                size="small"
                                appearance="subtle"
                                icon={<Edit20Regular />}
                                onClick={() => onEdit(group)}
                                title="Edit smart group"
                            />
                            <Button
                                size="small"
                                appearance="subtle"
                                icon={<Delete20Regular />}
                                onClick={() => onDelete(group.id)}
                                disabled={deletingSmartGroupId === group.id}
                                title="Delete smart group"
                            />
                        </div>
                    </div>
                ))
            )}
        </div>
    );
};
