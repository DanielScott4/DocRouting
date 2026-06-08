/*
# Initial Schema for PAdES Signing System

This migration creates the complete database schema for a multi-user PDF electronic signature routing system.

## Tables Created

1. **workflow_templates** - Reusable workflow definitions with multiple signing steps
   - `id` (uuid, primary key)
   - `name` (text, not null) - template name
   - `description` (text) - optional description
   - `is_archived` (boolean, default false) - soft delete flag
   - `user_id` (uuid, not null, references auth.users) - template owner
   - `created_at`, `updated_at` (timestamps)

2. **workflow_steps** - Individual steps within a workflow template
   - `id` (uuid, primary key)
   - `template_id` (uuid, FK to workflow_templates, cascade delete)
   - `order` (int, not null) - step order (same order = parallel)
   - `assignee_id` (uuid, FK to auth.users) - specific assigned user
   - `role_name` (text) - role-based routing when assignee_id is null
   - `is_parallel` (boolean, default false) - parallel execution flag
   - `deadline_hours` (int) - optional deadline
   - Signature field: `page_number`, `x`, `y`, `width`, `height`
   - `reason`, `location` (text) - signature metadata

3. **document_envelopes** - Document signing envelopes
   - `id` (uuid, primary key)
   - `template_id` (uuid, FK to workflow_templates)
   - `user_id` (uuid, not null, FK to auth.users) - uploader
   - `original_file_name` (text, not null)
   - `original_blob_path` (text, not null) - storage path of original
   - `working_blob_path` (text, not null) - current version with signatures
   - `status` (text, not null) - Draft, InProgress, Completed, Declined, Expired
   - `current_step_order` (int, default 0)
   - `created_at`, `updated_at` (timestamps)

4. **signature_records** - Applied signatures
   - `id` (uuid, primary key)
   - `envelope_id` (uuid, FK to document_envelopes, cascade delete)
   - `signed_by_user_id` (uuid, not null, FK to auth.users)
   - `step_order` (int, not null)
   - `signed_at` (timestamp, default now)
   - Certificate details: `certificate_subject`, `certificate_thumbprint`, `certificate_serial`
   - `ip_address` (text) - signer's IP
   - Signature field definition (same fields as workflow_steps)

5. **audit_entries** - Immutable audit log
   - `id` (uuid, primary key)
   - `envelope_id` (uuid, FK to document_envelopes, set null on delete)
   - `user_id` (uuid, FK to auth.users, set null on delete)
   - `action` (text, not null) - action type
   - `detail` (text) - additional details
   - `ip_address` (text)
   - `occurred_at` (timestamp, default now)

6. **signing_sessions** - Short-lived signing sessions (10-minute TTL)
   - `id` (uuid, primary key)
   - `envelope_id` (uuid, FK to document_envelopes)
   - `user_id` (uuid, not null, FK to auth.users)
   - `step_order` (int, not null)
   - `prepared_blob_path` (text, not null)
   - `digest_base64` (text, not null) - SHA-256 digest to sign
   - `digest_algorithm` (text, default 'SHA-256')
   - `byte_range` (int[]) - PDF byte range array
   - `expires_at` (timestamp, not null)
   - `created_at` (timestamp, default now)

## Security

- RLS enabled on all tables
- Owner-scoped policies for templates and envelopes (user can only access their own)
- Child records (steps, signatures) inherit access through parent
- Signing sessions scoped to the user
- Audit entries read-only after insert (append-only for compliance)

## Notes

1. `user_id` columns use `DEFAULT auth.uid()` so frontend inserts work without passing user_id
2. Parallel steps share the same `order` value in workflow_steps
3. Document envelopes grow incrementally (PAdES append-only)
4. Audit entries are retained even if envelope/user is deleted (SET NULL)
*/

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================================
-- WORKFLOW TEMPLATES
-- ============================================================================
CREATE TABLE IF NOT EXISTS workflow_templates (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name text NOT NULL,
    description text DEFAULT '',
    is_archived boolean NOT NULL DEFAULT false,
    user_id uuid NOT NULL DEFAULT auth.uid() REFERENCES auth.users(id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

ALTER TABLE workflow_templates ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "select_own_templates" ON workflow_templates;
CREATE POLICY "select_own_templates" ON workflow_templates FOR SELECT
    TO authenticated USING (auth.uid() = user_id);

DROP POLICY IF EXISTS "insert_own_templates" ON workflow_templates;
CREATE POLICY "insert_own_templates" ON workflow_templates FOR INSERT
    TO authenticated WITH CHECK (auth.uid() = user_id);

DROP POLICY IF EXISTS "update_own_templates" ON workflow_templates;
CREATE POLICY "update_own_templates" ON workflow_templates FOR UPDATE
    TO authenticated USING (auth.uid() = user_id) WITH CHECK (auth.uid() = user_id);

DROP POLICY IF EXISTS "delete_own_templates" ON workflow_templates;
CREATE POLICY "delete_own_templates" ON workflow_templates FOR DELETE
    TO authenticated USING (auth.uid() = user_id);

-- ============================================================================
-- WORKFLOW STEPS
-- ============================================================================
CREATE TABLE IF NOT EXISTS workflow_steps (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id uuid NOT NULL REFERENCES workflow_templates(id) ON DELETE CASCADE,
    "order" int NOT NULL,
    assignee_id uuid REFERENCES auth.users(id) ON DELETE SET NULL,
    role_name text,
    is_parallel boolean NOT NULL DEFAULT false,
    deadline_hours int,
    -- Signature field definition
    page_number int NOT NULL DEFAULT 1,
    x float NOT NULL DEFAULT 0,
    y float NOT NULL DEFAULT 0,
    width float NOT NULL DEFAULT 180,
    height float NOT NULL DEFAULT 60,
    reason text NOT NULL DEFAULT '',
    location text NOT NULL DEFAULT '',
    contact_info text DEFAULT ''
);

ALTER TABLE workflow_steps ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "select_steps_via_template" ON workflow_steps;
CREATE POLICY "select_steps_via_template" ON workflow_steps FOR SELECT
    TO authenticated USING (
        EXISTS (SELECT 1 FROM workflow_templates WHERE workflow_templates.id = workflow_steps.template_id AND workflow_templates.user_id = auth.uid())
    );

DROP POLICY IF EXISTS "insert_steps_via_template" ON workflow_steps;
CREATE POLICY "insert_steps_via_template" ON workflow_steps FOR INSERT
    TO authenticated WITH CHECK (
        EXISTS (SELECT 1 FROM workflow_templates WHERE workflow_templates.id = workflow_steps.template_id AND workflow_templates.user_id = auth.uid())
    );

DROP POLICY IF EXISTS "update_steps_via_template" ON workflow_steps;
CREATE POLICY "update_steps_via_template" ON workflow_steps FOR UPDATE
    TO authenticated USING (
        EXISTS (SELECT 1 FROM workflow_templates WHERE workflow_templates.id = workflow_steps.template_id AND workflow_templates.user_id = auth.uid())
    );

DROP POLICY IF EXISTS "delete_steps_via_template" ON workflow_steps;
CREATE POLICY "delete_steps_via_template" ON workflow_steps FOR DELETE
    TO authenticated USING (
        EXISTS (SELECT 1 FROM workflow_templates WHERE workflow_templates.id = workflow_steps.template_id AND workflow_templates.user_id = auth.uid())
    );

-- ============================================================================
-- DOCUMENT ENVELOPES
-- ============================================================================
CREATE TABLE IF NOT EXISTS document_envelopes (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id uuid REFERENCES workflow_templates(id) ON DELETE SET NULL,
    user_id uuid NOT NULL DEFAULT auth.uid() REFERENCES auth.users(id) ON DELETE CASCADE,
    original_file_name text NOT NULL,
    original_blob_path text NOT NULL,
    working_blob_path text NOT NULL,
    status text NOT NULL DEFAULT 'Draft',
    current_step_order int NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT valid_status CHECK (status IN ('Draft', 'InProgress', 'Completed', 'Declined', 'Expired'))
);

ALTER TABLE document_envelopes ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "select_own_envelopes" ON document_envelopes;
CREATE POLICY "select_own_envelopes" ON document_envelopes FOR SELECT
    TO authenticated USING (auth.uid() = user_id);

DROP POLICY IF EXISTS "insert_own_envelopes" ON document_envelopes;
CREATE POLICY "insert_own_envelopes" ON document_envelopes FOR INSERT
    TO authenticated WITH CHECK (auth.uid() = user_id);

DROP POLICY IF EXISTS "update_own_envelopes" ON document_envelopes;
CREATE POLICY "update_own_envelopes" ON document_envelopes FOR UPDATE
    TO authenticated USING (auth.uid() = user_id) WITH CHECK (auth.uid() = user_id);

DROP POLICY IF EXISTS "delete_own_envelopes" ON document_envelopes;
CREATE POLICY "delete_own_envelopes" ON document_envelopes FOR DELETE
    TO authenticated USING (auth.uid() = user_id);

-- ============================================================================
-- SIGNATURE RECORDS
-- ============================================================================
CREATE TABLE IF NOT EXISTS signature_records (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    envelope_id uuid NOT NULL REFERENCES document_envelopes(id) ON DELETE CASCADE,
    signed_by_user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE SET NULL,
    step_order int NOT NULL,
    signed_at timestamptz NOT NULL DEFAULT now(),
    certificate_subject text NOT NULL DEFAULT '',
    certificate_thumbprint text NOT NULL DEFAULT '',
    certificate_serial text NOT NULL DEFAULT '',
    ip_address text NOT NULL DEFAULT '',
    -- Signature field definition (captured at sign time)
    page_number int NOT NULL DEFAULT 1,
    x float NOT NULL DEFAULT 0,
    y float NOT NULL DEFAULT 0,
    width float NOT NULL DEFAULT 180,
    height float NOT NULL DEFAULT 60,
    reason text NOT NULL DEFAULT '',
    location text NOT NULL DEFAULT ''
);

ALTER TABLE signature_records ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "select_signatures_via_envelope" ON signature_records;
CREATE POLICY "select_signatures_via_envelope" ON signature_records FOR SELECT
    TO authenticated USING (
        EXISTS (SELECT 1 FROM document_envelopes WHERE document_envelopes.id = signature_records.envelope_id AND document_envelopes.user_id = auth.uid())
    );

DROP POLICY IF EXISTS "insert_signatures_via_envelope" ON signature_records;
CREATE POLICY "insert_signatures_via_envelope" ON signature_records FOR INSERT
    TO authenticated WITH CHECK (
        EXISTS (SELECT 1 FROM document_envelopes WHERE document_envelopes.id = signature_records.envelope_id AND document_envelopes.user_id = auth.uid())
    );

-- Note: Signatures should not be updated or deleted (append-only for compliance)
-- No UPDATE or DELETE policies

-- ============================================================================
-- AUDIT ENTRIES
-- ============================================================================
CREATE TABLE IF NOT EXISTS audit_entries (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    envelope_id uuid REFERENCES document_envelopes(id) ON DELETE SET NULL,
    user_id uuid REFERENCES auth.users(id) ON DELETE SET NULL,
    action text NOT NULL,
    detail text DEFAULT '',
    ip_address text DEFAULT '',
    occurred_at timestamptz NOT NULL DEFAULT now()
);

ALTER TABLE audit_entries ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "select_audit_via_envelope" ON audit_entries;
CREATE POLICY "select_audit_via_envelope" ON audit_entries FOR SELECT
    TO authenticated USING (
        envelope_id IS NULL OR
        EXISTS (SELECT 1 FROM document_envelopes WHERE document_envelopes.id = audit_entries.envelope_id AND document_envelopes.user_id = auth.uid())
    );

DROP POLICY IF EXISTS "insert_audit_entries" ON audit_entries;
CREATE POLICY "insert_audit_entries" ON audit_entries FOR INSERT
    TO authenticated WITH CHECK (true);

-- Audit entries are append-only - no UPDATE or DELETE policies

-- ============================================================================
-- SIGNING SESSIONS
-- ============================================================================
CREATE TABLE IF NOT EXISTS signing_sessions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    envelope_id uuid NOT NULL REFERENCES document_envelopes(id) ON DELETE CASCADE,
    user_id uuid NOT NULL DEFAULT auth.uid() REFERENCES auth.users(id) ON DELETE CASCADE,
    step_order int NOT NULL,
    prepared_blob_path text NOT NULL,
    digest_base64 text NOT NULL,
    digest_algorithm text NOT NULL DEFAULT 'SHA-256',
    byte_range int[] NOT NULL DEFAULT '{}',
    expires_at timestamptz NOT NULL DEFAULT (now() + interval '10 minutes'),
    created_at timestamptz NOT NULL DEFAULT now()
);

ALTER TABLE signing_sessions ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "select_own_sessions" ON signing_sessions;
CREATE POLICY "select_own_sessions" ON signing_sessions FOR SELECT
    TO authenticated USING (auth.uid() = user_id);

DROP POLICY IF EXISTS "insert_own_sessions" ON signing_sessions;
CREATE POLICY "insert_own_sessions" ON signing_sessions FOR INSERT
    TO authenticated WITH CHECK (auth.uid() = user_id);

DROP POLICY IF EXISTS "delete_own_sessions" ON signing_sessions;
CREATE POLICY "delete_own_sessions" ON signing_sessions FOR DELETE
    TO authenticated USING (auth.uid() = user_id);

-- ============================================================================
-- INDEXES
-- ============================================================================
CREATE INDEX IF NOT EXISTS idx_workflow_steps_template ON workflow_steps(template_id);
CREATE INDEX IF NOT EXISTS idx_document_envelopes_user ON document_envelopes(user_id);
CREATE INDEX IF NOT EXISTS idx_document_envelopes_status ON document_envelopes(status);
CREATE INDEX IF NOT EXISTS idx_signature_records_envelope ON signature_records(envelope_id);
CREATE INDEX IF NOT EXISTS idx_audit_entries_envelope ON audit_entries(envelope_id);
CREATE INDEX IF NOT EXISTS idx_audit_entries_user ON audit_entries(user_id);
CREATE INDEX IF NOT EXISTS idx_signing_sessions_envelope ON signing_sessions(envelope_id);
CREATE INDEX IF NOT EXISTS idx_signing_sessions_expires ON signing_sessions(expires_at);

-- ============================================================================
-- FUNCTIONS
-- ============================================================================

-- Function to clean up expired signing sessions
CREATE OR REPLACE FUNCTION cleanup_expired_sessions()
RETURNS void AS $$
BEGIN
    DELETE FROM signing_sessions WHERE expires_at < now();
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- Function to automatically update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Triggers for updated_at
DROP TRIGGER IF EXISTS update_workflow_templates_updated_at ON workflow_templates;
CREATE TRIGGER update_workflow_templates_updated_at
    BEFORE UPDATE ON workflow_templates
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_document_envelopes_updated_at ON document_envelopes;
CREATE TRIGGER update_document_envelopes_updated_at
    BEFORE UPDATE ON document_envelopes
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();