# ADR-0002: Server-derived hospital and branch scope

Status: accepted.

Decision: derive HospitalId/BranchId from authenticated membership, never from a trusted client header. Tenant-owned SQL predicates and composite foreign keys enforce ownership. UHID is hospital-unique; cross-hospital linkage is a separate consented/governed workflow.

Consequences: every repository query and integration adapter must carry scope. Negative tenant tests are release gates. Branch is an operational location, not an independent permission grant. The initial API is configured for one active hospital/branch; multi-branch selection and cross-hospital MPI remain planned.
