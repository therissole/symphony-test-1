CREATE TABLE openfga_tuple_outbox (
    id UUID PRIMARY KEY,
    sequence_number BIGINT GENERATED ALWAYS AS IDENTITY UNIQUE,
    operation VARCHAR(6) NOT NULL
        CHECK (operation IN ('write', 'delete')),
    tuple_user TEXT NOT NULL,
    tuple_relation TEXT NOT NULL,
    tuple_object TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    processed_at TIMESTAMPTZ,
    attempt_count INTEGER NOT NULL DEFAULT 0,
    last_error TEXT
);

CREATE INDEX idx_openfga_tuple_outbox_pending
    ON openfga_tuple_outbox (attempt_count, sequence_number)
    WHERE processed_at IS NULL;

CREATE INDEX idx_openfga_tuple_outbox_processed
    ON openfga_tuple_outbox (processed_at, sequence_number)
    WHERE processed_at IS NOT NULL;
