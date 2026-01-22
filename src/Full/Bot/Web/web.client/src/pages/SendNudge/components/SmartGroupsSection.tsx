import React from 'react';
import {
    Button,
    Text,
    Checkbox,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import {
    Sparkle20Regular,
    PeopleTeam20Regular,
    Settings20Regular,
} from '@fluentui/react-icons';
import { SmartGroupDto } from '../../../apimodels/Models';
import { useHistory } from 'react-router-dom';

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
    memberInfo: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
});

interface SmartGroupsSectionProps {
    smartGroups: SmartGroupDto[];
    selectedSmartGroupIds: string[];
    onToggle: (groupId: string, checked: boolean) => void;
}

export const SmartGroupsSection: React.FC<SmartGroupsSectionProps> = ({
    smartGroups,
    selectedSmartGroupIds,
    onToggle,
}) => {
    const styles = useStyles();
    const history = useHistory();

    return (
        <div className={styles.smartGroupSection}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: tokens.spacingVerticalS }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                    <Sparkle20Regular />
                    <Text weight="semibold">Select Smart Groups</Text>
                </div>
                <Button 
                    size="small" 
                    appearance="subtle"
                    icon={<Settings20Regular />} 
                    onClick={() => history.push('/smartgroups')}
                >
                    Manage Smart Groups
                </Button>
            </div>

            {smartGroups.length === 0 ? (
                <div style={{ textAlign: 'center', padding: tokens.spacingVerticalL }}>
                    <Text size={200}>No smart groups available.</Text>
                    <br />
                    <Button
                        size="small"
                        appearance="primary"
                        onClick={() => history.push('/smartgroups')}
                        style={{ marginTop: tokens.spacingVerticalS }}
                    >
                        Create Smart Group
                    </Button>
                </div>
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
                        <div className={styles.memberInfo}>
                            <PeopleTeam20Regular />
                            <Text size={200}>
                                {group.lastResolvedMemberCount != null 
                                    ? `${group.lastResolvedMemberCount} member${group.lastResolvedMemberCount !== 1 ? 's' : ''}` 
                                    : 'Not resolved yet'}
                            </Text>
                        </div>
                    </div>
                ))
            )}
        </div>
    );
};
