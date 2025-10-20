document.addEventListener('DOMContentLoaded', function () {
    // Project type section toggling
    const typeOfProjectSelect = document.getElementById('typeOfProjectSelect');
    const sections = {
        bir: document.getElementById('retainerBIRSection'),
        spp: document.getElementById('retainerSPPSection'),
        oneTime: document.getElementById('oneTimeTransactionSection'),
        audit: document.getElementById('externalAuditSection')
    };

    function showSection(type) {
        Object.values(sections).forEach(sec => sec.style.display = 'none');
        if (type === 'Retainership - BIR') sections.bir.style.display = '';
        if (type === 'Retainership - SPP') sections.spp.style.display = '';
        if (type === 'One Time Transaction') sections.oneTime.style.display = '';
        if (type === 'External Audit') sections.audit.style.display = '';
    }

    if (typeOfProjectSelect) {
        typeOfProjectSelect.addEventListener('change', function () {
            showSection(this.value);
            // Show/hide 'Other' project type input
            document.getElementById('otherTypeOfProjectDiv').style.display = (this.value === 'Other') ? '' : 'none';
        });
        // On page load, show correct section if editing
        showSection(typeOfProjectSelect.value);
    }

    // 'Other' logic for BIR RDO No.
    const birRdoNoSelect = document.getElementById('birRdoNoSelect');
    if (birRdoNoSelect) {
        birRdoNoSelect.addEventListener('change', function () {
            document.getElementById('otherBirRdoNoDiv').style.display = (this.value === 'Other') ? '' : 'none';
        });
    }

    // 'Other' logic for BIR Catch-Up Reason
    const catchUpReasonsSelect = document.getElementById('catchUpReasonsSelect');
    if (catchUpReasonsSelect) {
        catchUpReasonsSelect.addEventListener('change', function () {
            let show = false;
            Array.from(this.selectedOptions).forEach(opt => {
                if (opt.value === 'Other') show = true;
            });
            document.getElementById('otherCatchUpReasonDiv').style.display = show ? '' : 'none';
        });
    }

    // 'Other' logic for BIR Compliance Activities
    const birComplianceActivitiesSelect = document.getElementById('birComplianceActivitiesSelect');
    if (birComplianceActivitiesSelect) {
        birComplianceActivitiesSelect.addEventListener('change', function () {
            let show = false;
            Array.from(this.selectedOptions).forEach(opt => {
                if (opt.value === 'Other') show = true;
            });
            document.getElementById('otherBIRComplianceDiv').style.display = show ? '' : 'none';
        });
    }

    // 'Other' logic for SPP Compliance Activities
    const sppComplianceActivitiesSelect = document.getElementById('sppComplianceActivitiesSelect');
    if (sppComplianceActivitiesSelect) {
        sppComplianceActivitiesSelect.addEventListener('change', function () {
            let show = false;
            Array.from(this.selectedOptions).forEach(opt => {
                if (opt.value === 'Other') show = true;
            });
            document.getElementById('otherSPPComplianceDiv').style.display = show ? '' : 'none';
        });
    }

    // 'Other' logic for One Time Transaction Area of Services
    const oneTimeAreaOfServicesSelect = document.getElementById('oneTimeAreaOfServicesSelect');
    if (oneTimeAreaOfServicesSelect) {
        oneTimeAreaOfServicesSelect.addEventListener('change', function () {
            document.getElementById('oneTimeOtherAreaOfServicesDiv').style.display = (this.value === 'Other') ? '' : 'none';
        });
    }

    // 'Other' logic for External Audit Purposes
    const externalAuditPurposesSelect = document.getElementById('externalAuditPurposesSelect');
    if (externalAuditPurposesSelect) {
        externalAuditPurposesSelect.addEventListener('change', function () {
            let show = false;
            Array.from(this.selectedOptions).forEach(opt => {
                if (opt.value === 'Other') show = true;
            });
            document.getElementById('externalAuditOtherPurposeDiv').style.display = show ? '' : 'none';
        });
    }

    // Requesting Party 'Other' logic
    const requestingPartySelect = document.getElementById('requestingPartySelect');
    if (requestingPartySelect) {
        requestingPartySelect.addEventListener('change', function () {
            document.getElementById('otherRequestingPartyDiv').style.display = (this.value === 'Other' || this.value === 'Referral by') ? '' : 'none';
        });
    }
}); 