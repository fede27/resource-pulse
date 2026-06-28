import { server } from '@/test/msw/server';
import { getLoadBandsGetMockHandler } from '@/api/generated/load-bands/load-bands.msw';
import { getTimeFenceGetMockHandler } from '@/api/generated/time-fence/time-fence.msw';
import { getBucketingGetMockHandler } from '@/api/generated/bucketing/bucketing.msw';
import { getCommitmentPolicyGetMockHandler } from '@/api/generated/commitment-policy/commitment-policy.msw';
import {
  BucketGrain,
  CommitmentLevel,
  DurationUnit,
  type BucketingDefaultsDto,
  type CommitmentPolicyDto,
  type LoadBandConfigurationDto,
  type TimeFenceConfigurationDto,
} from '@/api/generated/schemas';

// Valid, opinionated-default config fixtures shared by the settings tests.
// Lives under src/test (excluded from the production build and from coverage).
export const loadBands: LoadBandConfigurationDto = {
  bands: [
    { label: 'Healthy', lowerBound: 0 },
    { label: 'Over', lowerBound: 100 },
  ],
};
export const timeFence: TimeFenceConfigurationDto = {
  frozenHorizon: { value: 2, unit: DurationUnit.Weeks },
  slushyHorizon: { value: 2, unit: DurationUnit.Months },
};
export const bucketing: BucketingDefaultsDto = {
  primaryGrain: BucketGrain.Week,
  secondaryGrain: BucketGrain.Month,
};
export const commitment: CommitmentPolicyDto = {
  hardCommitLevels: [CommitmentLevel.Committed, CommitmentLevel.Critical],
};

export function seedConfig(): void {
  server.use(
    getLoadBandsGetMockHandler(loadBands),
    getTimeFenceGetMockHandler(timeFence),
    getBucketingGetMockHandler(bucketing),
    getCommitmentPolicyGetMockHandler(commitment),
  );
}
