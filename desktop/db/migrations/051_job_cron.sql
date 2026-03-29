-- Migration 051: Add cron expression support to job scheduler
ALTER TABLE job_schedules ADD COLUMN IF NOT EXISTS schedule_cron varchar(64) DEFAULT '';
