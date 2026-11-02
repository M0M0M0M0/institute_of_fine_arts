IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [migration_id] nvarchar(150) NOT NULL,
        [product_version] nvarchar(32) NOT NULL,
        CONSTRAINT [pk___ef_migrations_history] PRIMARY KEY ([migration_id])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE TABLE [account] (
        [id] bigint NOT NULL IDENTITY,
        [username] varchar(50) NOT NULL,
        [password_hash] varchar(100) NOT NULL,
        [full_name] nvarchar(100) NOT NULL,
        [email] varchar(100) NOT NULL,
        [phone] varchar(20) NULL,
        [date_of_birth] date NULL,
        [gender] char(1) NULL,
        [address] nvarchar(255) NULL,
        [role] varchar(20) NOT NULL,
        [active] bit NOT NULL,
        [last_login_at] datetime2(0) NULL,
        [created_at] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
        [updated_at] datetime2(0) NULL,
        CONSTRAINT [pk_account] PRIMARY KEY ([id]),
        CONSTRAINT [CK_account_gender] CHECK ([gender] IS NULL OR [gender] IN ('M','F','O')),
        CONSTRAINT [CK_account_role] CHECK ([role] IN ('ADMIN','MANAGER','STAFF','STUDENT'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE TABLE [customer] (
        [id] bigint NOT NULL IDENTITY,
        [full_name] nvarchar(100) NOT NULL,
        [email] varchar(100) NULL,
        [phone] varchar(20) NULL,
        [address] nvarchar(255) NULL,
        [notes] nvarchar(500) NULL,
        [created_at] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
        [updated_at] datetime2(0) NULL,
        CONSTRAINT [pk_customer] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE TABLE [staff_profile] (
        [id] bigint NOT NULL IDENTITY,
        [account_id] bigint NOT NULL,
        [staff_code] varchar(20) NOT NULL,
        [position] nvarchar(100) NULL,
        [joined_date] date NOT NULL,
        [classes_in_charge] nvarchar(255) NULL,
        [subjects_in_charge] nvarchar(255) NULL,
        [qualification] nvarchar(255) NULL,
        [notes] nvarchar(max) NULL,
        [created_at] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
        [updated_at] datetime2(0) NULL,
        CONSTRAINT [pk_staff_profile] PRIMARY KEY ([id]),
        CONSTRAINT [fk_staff_profile_account_account_id] FOREIGN KEY ([account_id]) REFERENCES [account] ([id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE TABLE [student_profile] (
        [id] bigint NOT NULL IDENTITY,
        [account_id] bigint NOT NULL,
        [student_code] varchar(20) NOT NULL,
        [enrollment_date] date NOT NULL,
        [course_name] nvarchar(100) NOT NULL,
        [class_name] nvarchar(50) NULL,
        [enrollment_status] varchar(20) NOT NULL,
        [notes] nvarchar(max) NULL,
        [created_at] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
        [updated_at] datetime2(0) NULL,
        CONSTRAINT [pk_student_profile] PRIMARY KEY ([id]),
        CONSTRAINT [CK_student_profile_status] CHECK ([enrollment_status] IN ('STUDYING','GRADUATED','SUSPENDED','DROPPED')),
        CONSTRAINT [fk_student_profile_account_account_id] FOREIGN KEY ([account_id]) REFERENCES [account] ([id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE TABLE [competition] (
        [id] bigint NOT NULL IDENTITY,
        [code] varchar(20) NOT NULL,
        [title] nvarchar(200) NOT NULL,
        [description] nvarchar(max) NULL,
        [rules] nvarchar(max) NULL,
        [start_date] date NOT NULL,
        [end_date] date NOT NULL,
        [status] varchar(20) NOT NULL,
        [published_at] datetime2(0) NULL,
        [published_by] bigint NULL,
        [cover_image_path] varchar(500) NULL,
        [result_remarks] nvarchar(max) NULL,
        [created_by] bigint NOT NULL,
        [created_at] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
        [updated_at] datetime2(0) NULL,
        CONSTRAINT [pk_competition] PRIMARY KEY ([id]),
        CONSTRAINT [CK_competition_dates] CHECK ([end_date] >= [start_date]),
        CONSTRAINT [CK_competition_status] CHECK ([status] IN ('UPCOMING','ONGOING','CLOSED','GRADED')),
        CONSTRAINT [fk_competition_staff_profiles_created_by] FOREIGN KEY ([created_by]) REFERENCES [staff_profile] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_competition_staff_profiles_published_by] FOREIGN KEY ([published_by]) REFERENCES [staff_profile] ([id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE TABLE [exhibition] (
        [id] bigint NOT NULL IDENTITY,
        [name] nvarchar(200) NOT NULL,
        [description] nvarchar(max) NULL,
        [venue] nvarchar(255) NOT NULL,
        [city] nvarchar(100) NOT NULL,
        [organizer] nvarchar(200) NULL,
        [start_date] date NOT NULL,
        [end_date] date NOT NULL,
        [status] varchar(20) NOT NULL,
        [created_by] bigint NOT NULL,
        [created_at] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
        [updated_at] datetime2(0) NULL,
        CONSTRAINT [pk_exhibition] PRIMARY KEY ([id]),
        CONSTRAINT [CK_exhibition_dates] CHECK ([end_date] >= [start_date]),
        CONSTRAINT [CK_exhibition_status] CHECK ([status] IN ('UPCOMING','ONGOING','FINISHED')),
        CONSTRAINT [fk_exhibition_staff_profiles_created_by] FOREIGN KEY ([created_by]) REFERENCES [staff_profile] ([id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE TABLE [competition_award] (
        [id] bigint NOT NULL IDENTITY,
        [competition_id] bigint NOT NULL,
        [award_name] nvarchar(100) NOT NULL,
        [rank_no] int NOT NULL,
        [prize_value] decimal(18,2) NOT NULL,
        [prize_description] nvarchar(500) NULL,
        [quantity] int NOT NULL,
        [created_at] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
        [updated_at] datetime2(0) NULL,
        CONSTRAINT [pk_competition_award] PRIMARY KEY ([id]),
        CONSTRAINT [CK_competition_award_prize] CHECK ([prize_value] >= 0),
        CONSTRAINT [CK_competition_award_quantity] CHECK ([quantity] > 0),
        CONSTRAINT [CK_competition_award_rank] CHECK ([rank_no] >= 1),
        CONSTRAINT [fk_competition_award_competitions_competition_id] FOREIGN KEY ([competition_id]) REFERENCES [competition] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE TABLE [submission] (
        [id] bigint NOT NULL IDENTITY,
        [competition_id] bigint NOT NULL,
        [student_id] bigint NOT NULL,
        [title] nvarchar(150) NOT NULL,
        [image_path] varchar(500) NOT NULL,
        [thumbnail_path] varchar(500) NULL,
        [original_file_name] nvarchar(255) NOT NULL,
        [file_size] bigint NOT NULL,
        [text_type] varchar(20) NOT NULL,
        [text_content] nvarchar(4000) NOT NULL,
        [description] nvarchar(2000) NOT NULL,
        [submitted_at] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
        [created_at] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
        [updated_at] datetime2(0) NULL,
        CONSTRAINT [pk_submission] PRIMARY KEY ([id]),
        CONSTRAINT [CK_submission_size] CHECK ([file_size] > 0),
        CONSTRAINT [CK_submission_text_type] CHECK ([text_type] IN ('POEM','QUOTATION','STORY','OTHER')),
        CONSTRAINT [fk_submission_competition_competition_id] FOREIGN KEY ([competition_id]) REFERENCES [competition] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_submission_student_profile_student_id] FOREIGN KEY ([student_id]) REFERENCES [student_profile] ([id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE TABLE [award_issued] (
        [id] bigint NOT NULL IDENTITY,
        [competition_award_id] bigint NOT NULL,
        [submission_id] bigint NOT NULL,
        [issued_by] bigint NOT NULL,
        [issued_date] date NOT NULL DEFAULT (CAST(SYSDATETIME() AS date)),
        [remarks] nvarchar(max) NULL,
        [created_at] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
        [updated_at] datetime2(0) NULL,
        CONSTRAINT [pk_award_issued] PRIMARY KEY ([id]),
        CONSTRAINT [fk_award_issued_competition_awards_competition_award_id] FOREIGN KEY ([competition_award_id]) REFERENCES [competition_award] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_award_issued_staff_profiles_issued_by] FOREIGN KEY ([issued_by]) REFERENCES [staff_profile] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_award_issued_submissions_submission_id] FOREIGN KEY ([submission_id]) REFERENCES [submission] ([id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE TABLE [evaluation] (
        [id] bigint NOT NULL IDENTITY,
        [submission_id] bigint NOT NULL,
        [evaluated_by] bigint NOT NULL,
        [rating] varchar(20) NOT NULL,
        [positive_points] nvarchar(max) NULL,
        [negative_points] nvarchar(max) NULL,
        [improvement_areas] nvarchar(max) NULL,
        [evaluated_at] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
        [created_at] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
        [updated_at] datetime2(0) NULL,
        CONSTRAINT [pk_evaluation] PRIMARY KEY ([id]),
        CONSTRAINT [CK_evaluation_dq_reason] CHECK ([rating] <> 'DISQUALIFIED' OR [negative_points] IS NOT NULL),
        CONSTRAINT [CK_evaluation_rating] CHECK ([rating] IN ('BEST','BETTER','GOOD','MODERATE','NORMAL','DISQUALIFIED')),
        CONSTRAINT [fk_evaluation_staff_profiles_evaluated_by] FOREIGN KEY ([evaluated_by]) REFERENCES [staff_profile] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_evaluation_submissions_submission_id] FOREIGN KEY ([submission_id]) REFERENCES [submission] ([id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE TABLE [exhibition_entry] (
        [id] bigint NOT NULL IDENTITY,
        [exhibition_id] bigint NOT NULL,
        [submission_id] bigint NOT NULL,
        [asking_price] decimal(18,2) NOT NULL,
        [status] varchar(20) NOT NULL,
        [reject_reason] nvarchar(500) NULL,
        [sold_price] decimal(18,2) NULL,
        [sold_date] date NULL,
        [customer_id] bigint NULL,
        [paid_to_student] bit NOT NULL,
        [paid_amount] decimal(18,2) NULL,
        [paid_date] date NULL,
        [proposed_by] bigint NOT NULL,
        [reviewed_by] bigint NULL,
        [notes] nvarchar(500) NULL,
        [created_at] datetime2(0) NOT NULL DEFAULT (SYSDATETIME()),
        [updated_at] datetime2(0) NULL,
        CONSTRAINT [pk_exhibition_entry] PRIMARY KEY ([id]),
        CONSTRAINT [CK_entry_asking_price] CHECK ([asking_price] > 0),
        CONSTRAINT [CK_entry_paid] CHECK ([paid_to_student] = 0 OR ([status] = 'SOLD' AND [paid_amount] IS NOT NULL AND [paid_date] IS NOT NULL)),
        CONSTRAINT [CK_entry_paid_amount] CHECK ([paid_amount] IS NULL OR [paid_amount] >= 0),
        CONSTRAINT [CK_entry_paid_date] CHECK ([paid_date] IS NULL OR [sold_date] IS NULL OR [paid_date] >= [sold_date]),
        CONSTRAINT [CK_entry_reject] CHECK ([status] <> 'REJECTED' OR [reject_reason] IS NOT NULL),
        CONSTRAINT [CK_entry_sold] CHECK ([status] <> 'SOLD' OR ([sold_price] IS NOT NULL AND [sold_date] IS NOT NULL AND [customer_id] IS NOT NULL)),
        CONSTRAINT [CK_entry_sold_price] CHECK ([sold_price] IS NULL OR [sold_price] > 0),
        CONSTRAINT [CK_entry_status] CHECK ([status] IN ('PENDING','ACCEPTED','REJECTED','SOLD','UNSOLD')),
        CONSTRAINT [fk_exhibition_entry_account_proposed_by] FOREIGN KEY ([proposed_by]) REFERENCES [account] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_exhibition_entry_customer_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customer] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_exhibition_entry_exhibition_exhibition_id] FOREIGN KEY ([exhibition_id]) REFERENCES [exhibition] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_exhibition_entry_staff_profiles_reviewed_by] FOREIGN KEY ([reviewed_by]) REFERENCES [staff_profile] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [fk_exhibition_entry_submissions_submission_id] FOREIGN KEY ([submission_id]) REFERENCES [submission] ([id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_account_role] ON [account] ([role]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_account_email] ON [account] ([email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_account_username] ON [account] ([username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_award_issued_competition_award_id] ON [award_issued] ([competition_award_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_award_issued_issued_by] ON [award_issued] ([issued_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_award_issued_submission] ON [award_issued] ([submission_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_competition_created_by] ON [competition] ([created_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_competition_published_by] ON [competition] ([published_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_competition_status_dates] ON [competition] ([status], [start_date], [end_date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_competition_code] ON [competition] ([code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_competition_award_name] ON [competition_award] ([competition_id], [award_name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_customer_phone] ON [customer] ([phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_evaluation_evaluated_by] ON [evaluation] ([evaluated_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_evaluation_rating] ON [evaluation] ([rating]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_evaluation_submission] ON [evaluation] ([submission_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_exhibition_created_by] ON [exhibition] ([created_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_entry_exhibition_status] ON [exhibition_entry] ([exhibition_id], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_exhibition_entry_customer_id] ON [exhibition_entry] ([customer_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_exhibition_entry_proposed_by] ON [exhibition_entry] ([proposed_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_exhibition_entry_reviewed_by] ON [exhibition_entry] ([reviewed_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_entry_active_submission] ON [exhibition_entry] ([submission_id]) WHERE [status] IN (''PENDING'',''ACCEPTED'')');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_staff_profile_account] ON [staff_profile] ([account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_staff_profile_staff_code] ON [staff_profile] ([staff_code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_student_profile_class_name] ON [student_profile] ([class_name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_student_profile_account] ON [student_profile] ([account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_student_profile_student_code] ON [student_profile] ([student_code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_submission_competition_submitted] ON [submission] ([competition_id], [submitted_at]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_submission_student] ON [submission] ([student_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_submission_competition_student] ON [submission] ([competition_id], [student_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [migration_id] = N'20261102113304_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([migration_id], [product_version])
    VALUES (N'20261102113304_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

