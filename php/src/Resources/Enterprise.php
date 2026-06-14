<?php

declare(strict_types=1);

namespace EPostak\Resources;

class EnterprisePull
{
    public function __construct(
        public Inbound $inbound,
        public Outbound $outbound
    ) {
    }
}

class Enterprise
{
    public Inbox $inbox;
    public EnterprisePull $pull;

    public function __construct(
        public Auth $auth,
        public Audit $audit,
        public Documents $documents,
        public Firms $firms,
        public Peppol $peppol,
        public Webhooks $webhooks,
        public Reporting $reporting,
        public Extract $extract,
        public Account $account,
        public Integrator $integrator,
        public Connector $connector,
        Inbound $inbound,
        Outbound $outbound
    ) {
        $this->inbox = $documents->inbox;
        $this->pull = new EnterprisePull($inbound, $outbound);
    }
}
