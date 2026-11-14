# ADR-0003: Signed clinical and issued financial records are immutable

Status: accepted; amendment workflows pending.

Decision: clinical sign-off binds canonical content with a SHA-256 hash and locks the record. SQL triggers and repository state/version checks defend against overwrites. Issued invoice content and captured payments are append-only. Corrections require linked addenda/credit notes with authorization and audit.

Consequences: the current slice cannot amend signed content; that workflow must be completed before production use. A content hash is not a certificate-based signature. Signature keys, trusted identity, timestamping and verification belong in a dedicated signing integration. Retention/disposal follows approved policy, not ad hoc deletion.
