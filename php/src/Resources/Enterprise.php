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
    public Connector $connector;

    public function __construct(
        public Auth $auth,
        public Box $box,
        public Audit $audit,
        public Documents $documents,
        public Firms $firms,
        public Peppol $peppol,
        public Webhooks $webhooks,
        public Reporting $reporting,
        public Extract $extract,
        public Account $account,
        public Integrator $integrator,
        Connector $connector,
        public Payloads $payloads,
        public Events $events,
        Inbound $inbound,
        Outbound $outbound
    ) {
        $this->connector = $connector;
        $this->inbox = $documents->inbox;
        $this->pull = new EnterprisePull($inbound, $outbound);
    }
}
