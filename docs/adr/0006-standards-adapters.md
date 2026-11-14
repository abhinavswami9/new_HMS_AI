# ADR-0006: Standards-based interoperability behind module ports

Status: accepted design; adapters not implemented.

Decision: map internal records to versioned FHIR/HL7/DICOM/ABDM contracts in infrastructure adapters. Keep StudyInstanceUID/accession/specimen/order identities distinct. Use consent artefacts for ABDM exchange, controlled viewer launches for PACS and server-verified idempotent webhooks for payments.

Consequences: integration-ready schemas are not conformance. Each adapter needs contract/sandbox tests, reconciliation, retry/dead-letter handling, terminology provenance and operational ownership. Do not store diagnostic DICOM pixels in ordinary clinical rows or advertise a viewer as diagnostic without appropriate validation.
