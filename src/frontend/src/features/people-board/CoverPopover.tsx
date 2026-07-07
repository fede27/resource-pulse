import { useMemo, useState } from 'react';
import { App, Popover, Select, Spin } from 'antd';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { useQueryClient } from '@tanstack/react-query';
import { getAllocationsGetForResourceQueryKey } from '@/api/generated/allocations/allocations';
import { getLoadGetOpenDemandsQueryKey, useLoadGetOpenDemands } from '@/api/generated/load/load';
import { usePlanCommandsExecute } from '@/api/generated/plan-commands/plan-commands';
import { useRolesGetAll } from '@/api/generated/roles/roles';
import {
  AllocationStatus,
  PlanChangeKind,
  type CoverInferredCommand,
  type CreateCommand,
  type PlanDemandChange,
  type RoleReadDto,
} from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import { parseDurationHours } from '@/lib/duration';
import {
  capacityInWindow,
  proposalPercent,
  toOpenDemand,
  type OpenDemand,
  type PersonData,
} from './peopleBoardModel';
import { projectHue } from './projectHue';
import type { RootProjectOption } from './usePeopleBoard';
import { useStyles } from './CoverPopover.styles';

export type PendingRange = { from: string; toExcl: string; anchorX: number };

export type CoverPopoverProps = {
  person: PersonData;
  pending: PendingRange;
  rootProjects: RootProjectOption[];
  onClose: () => void;
};

// The wire format of POST /api/plan/commands requires the System.Text.Json
// polymorphism discriminator `kind`, which Swashbuckle does not surface in the
// schema (so the orval types lack the property) — it is injected here, in one
// place, never inline at call sites.
function kinded<T extends object>(kind: 'create' | 'coverInferred', body: T): T {
  return { kind, ...body } as T;
}

// Drag-to-cover picker: free capacity lands ON an open demand (attach-first),
// or materializes an INFERRED demand on a chosen project (coverInferred, C3).
// Every proposal is Tentative — promotion to Hard is a separate, explicit
// gesture. Open demands come from GET /api/demands/open filtered by the
// person's role (a person without a role picks the role explicitly).
export function CoverPopover({ person, pending, rootProjects, onClose }: CoverPopoverProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const { message } = App.useApp();
  const showApiError = useApiError();
  const queryClient = useQueryClient();

  const toIncl = dayjs(pending.toExcl).subtract(1, 'day').format('YYYY-MM-DD');
  const weeks = dayjs(pending.toExcl).diff(dayjs(pending.from), 'day') / 7;
  const capH = useMemo(
    () => capacityInWindow(person.capacityByDay, pending.from, toIncl),
    [person.capacityByDay, pending.from, toIncl],
  );

  // Role for the demand filter / the inferred materialization. Defaults to the
  // person's own role; explicit choice when the person has none.
  const [pickedRoleId, setPickedRoleId] = useState<string | null>(null);
  const roleId = person.person.roleId ?? pickedRoleId;

  const rolesQ = useRolesGetAll({ query: { enabled: person.person.roleId === null } });
  const roleOptions = useMemo(() => {
    const rows = (rolesQ.data?.data ?? []) as RoleReadDto[];
    return rows
      .filter((r) => r.id)
      .map((r) => ({ value: r.id!, label: r.name ?? '—' }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }, [rolesQ.data]);

  const openQ = useLoadGetOpenDemands(
    { from: pending.from, to: toIncl, ...(roleId ? { roleId } : {}) },
    { query: { enabled: true } },
  );
  const demands = useMemo(() => (openQ.data ?? []).map(toOpenDemand), [openQ.data]);

  // coverInferred with >1 uncovered candidates commits nothing and returns the
  // candidate list (PlanChangeKind.Candidate) — the user picks which one.
  const [candidates, setCandidates] = useState<PlanDemandChange[] | null>(null);

  const mutation = usePlanCommandsExecute({
    mutation: {
      onError: (e) => showApiError(e),
    },
  });

  const invalidateAndClose = (pct: number, projectName: string, inferred: boolean) => {
    void queryClient.invalidateQueries({
      queryKey: getAllocationsGetForResourceQueryKey(person.person.id),
      exact: false,
    });
    void queryClient.invalidateQueries({ queryKey: getLoadGetOpenDemandsQueryKey(), exact: false });
    message.success(
      `${t(inferred ? 'peopleBoard.picker.successInferred' : 'peopleBoard.picker.successCover')} — ${t('peopleBoard.picker.successBody', { pct, name: person.person.name, project: projectName })}`,
    );
    onClose();
  };

  const coverDemand = (d: OpenDemand) => {
    const pct = proposalPercent(d.residualH, capH);
    mutation.mutate(
      {
        data: kinded<CreateCommand>('create', {
          demandId: d.demandId,
          resourceId: person.person.id,
          periodStart: pending.from,
          periodEnd: toIncl,
          percent: pct,
          status: AllocationStatus.Tentative,
        }),
      },
      { onSuccess: () => invalidateAndClose(pct, d.rootProjectName, false) },
    );
  };

  const coverInferred = (project: RootProjectOption) => {
    if (!roleId) return;
    const pct = proposalPercent(null, capH);
    mutation.mutate(
      {
        data: kinded<CoverInferredCommand>('coverInferred', {
          projectNodeId: project.id,
          roleId,
          resourceId: person.person.id,
          periodStart: pending.from,
          periodEnd: toIncl,
          percent: pct,
          status: AllocationStatus.Tentative,
        }),
      },
      {
        onSuccess: (res) => {
          const cands = (res.demandChanges ?? []).filter((c) => c.kind === PlanChangeKind.Candidate);
          if (!res.committed && cands.length > 0) {
            setCandidates(cands);
            return;
          }
          invalidateAndClose(pct, project.name, true);
        },
      },
    );
  };

  const coverCandidate = (c: PlanDemandChange) => {
    const required = c.requiredHours != null ? parseDurationHours(c.requiredHours) : null;
    const pct = proposalPercent(required, capH);
    mutation.mutate(
      {
        data: kinded<CreateCommand>('create', {
          demandId: c.id ?? '',
          resourceId: person.person.id,
          periodStart: pending.from,
          periodEnd: toIncl,
          percent: pct,
          status: AllocationStatus.Tentative,
        }),
      },
      {
        onSuccess: () => {
          const project = rootProjects.find((p) => c.projectNodeId && p.id === c.projectNodeId);
          invalidateAndClose(pct, project?.name ?? '—', false);
        },
      },
    );
  };

  // AntD Popover does the placement work: portaled to body (no clipping from
  // the board's scroll container) and auto-flipped by `autoAdjustOverflow`
  // when the space below the release point isn't enough.
  const card = (
    <div className={styles.card}>
      <div className={styles.header}>
            <div className={styles.headerTitle}>
              {t('peopleBoard.picker.title', { name: person.person.name })}
              {person.person.roleName ? ` · ${person.person.roleName}` : ''}
            </div>
            <div className={styles.headerSub}>
              {t('peopleBoard.picker.subtitleRange', {
                from: dayjs(pending.from).format('D MMM'),
                to: dayjs(toIncl).format('D MMM'),
                weeks: weeks.toFixed(1),
              })}
            </div>
          </div>

          <div className={styles.body}>
            {person.person.roleId === null && (
              <div className={styles.roleSelectRow}>
                <span>{t('peopleBoard.picker.roleLabel')}</span>
                <Select
                  size="small"
                  className={styles.roleSelect}
                  placeholder={t('peopleBoard.picker.rolePlaceholder')}
                  value={pickedRoleId}
                  onChange={(v) => setPickedRoleId(v)}
                  options={roleOptions}
                  loading={rolesQ.isPending}
                />
              </div>
            )}

            {candidates ? (
              <>
                <div className={styles.sectionTitle}>{t('peopleBoard.picker.candidatesTitle')}</div>
                {candidates.map((c) => {
                  const required = c.requiredHours != null ? parseDurationHours(c.requiredHours) : null;
                  return (
                    <div key={c.id} className={styles.demandRow} onClick={() => coverCandidate(c)}>
                      <div className={styles.demandText}>
                        <div className={styles.demandProject}>
                          {required !== null
                            ? t('peopleBoard.picker.candidateTarget', { hours: Math.round(required) })
                            : t('peopleBoard.picker.candidateBestEffort')}
                        </div>
                        {c.notes && <div className={styles.demandBestEffort}>{c.notes}</div>}
                      </div>
                      <span className={styles.coverWord}>{t('peopleBoard.picker.cover')}</span>
                    </div>
                  );
                })}
              </>
            ) : (
              <>
                <div className={styles.sectionTitle}>
                  {person.person.roleName
                    ? t('peopleBoard.picker.openDemands', { role: person.person.roleName })
                    : t('peopleBoard.picker.openDemandsAll')}
                </div>
                {openQ.isPending && (
                  <div className={styles.emptyNote}>
                    <Spin size="small" />
                  </div>
                )}
                {!openQ.isPending && demands.length === 0 && (
                  <div className={styles.emptyNote}>
                    {person.person.roleName
                      ? t('peopleBoard.picker.none', { role: person.person.roleName })
                      : t('peopleBoard.picker.noneAll')}
                  </div>
                )}
                {demands.map((d) => {
                  const hue = projectHue(d.rootProjectId);
                  return (
                    <div key={d.demandId} className={styles.demandRow} onClick={() => coverDemand(d)}>
                      {/* dynamic: per-project accent. */}
                      <span className={styles.demandDot} style={{ background: hue.accent }} />
                      <div className={styles.demandText}>
                        <div className={styles.demandProject}>{d.rootProjectName}</div>
                        {d.residualH !== null ? (
                          <div className={styles.demandResidual}>
                            {t('peopleBoard.picker.residual', { hours: Math.round(d.residualH), role: d.roleName })}
                          </div>
                        ) : (
                          <div className={styles.demandBestEffort}>
                            {t('peopleBoard.picker.bestEffort', { role: d.roleName })}
                          </div>
                        )}
                      </div>
                      <span className={styles.coverWord}>{t('peopleBoard.picker.cover')}</span>
                    </div>
                  );
                })}

                <div className={styles.sectionTitle}>{t('peopleBoard.picker.inferredTitle')}</div>
                {!roleId && (
                  <div className={styles.emptyNote}>{t('peopleBoard.picker.noRoleWarning', { name: person.person.name })}</div>
                )}
                <div className={styles.chips}>
                  {rootProjects.map((p) => {
                    const hue = projectHue(p.id);
                    return (
                      <button
                        key={p.id}
                        type="button"
                        className={styles.projectChip}
                        disabled={!roleId || mutation.isPending}
                        // dynamic: per-project accent on the chip.
                        style={{ border: `1px solid ${hue.accent}55`, color: hue.text, opacity: roleId ? 1 : 0.5 }}
                        onClick={() => coverInferred(p)}
                      >
                        {/* dynamic: per-project accent. */}
                        <span className={styles.chipDot} style={{ background: hue.accent }} />
                        {p.name}
                      </button>
                    );
                  })}
                </div>
          </>
        )}
      </div>
    </div>
  );

  return (
    <Popover
      open
      trigger="click"
      placement="bottomLeft"
      autoAdjustOverflow
      arrow={false}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      classNames={{ container: styles.popContainer }}
      content={card}
    >
      <span className={styles.anchor} />
    </Popover>
  );
}
